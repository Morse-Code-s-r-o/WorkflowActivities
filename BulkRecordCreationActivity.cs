using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class QueryExtensions
{
    public static void ProcessQuery<T>(this IOrganizationService service, QueryExpression query, Action<T> processAction, bool usePageingCookie = true) where T : Entity
    {
        if (query.PageInfo == null)
        {
            query.PageInfo = new PagingInfo { Count = 200, PageNumber = 1 };
        }

        if (query.PageInfo.Count == 0)
        {
            query.PageInfo.Count = 200;
        }

        if (query.PageInfo.PageNumber <= 0)
        {
            query.PageInfo.PageNumber = 1;
        }

        EntityCollection result;
        do
        {
            result = service.RetrieveMultiple(query);

            foreach (T entity in result.Entities.Select(x => x.ToEntity<T>()))
            {
                processAction(entity);
            }

            query.PageInfo.PageNumber += 1;
            if (usePageingCookie)
            {
                query.PageInfo.PagingCookie = result.PagingCookie;
            }

        } while (result.MoreRecords);
    }
}

public class BulkRecordCreationActivity : CodeActivity
{
    [Output("ReturnState")]
    public OutArgument<bool> ReturnState { get; set; }

    [Output("ReturnText")]
    public OutArgument<string> ReturnText { get; set; }

    [Input("FetchXML")]
    [RequiredArgument]
    public InArgument<string> FetchXMLInput { get; set; }

    [Input("Configuration")]
    [ReferenceTarget("dsa_bulkrecordcreationconfiguration")]
    [RequiredArgument]
    public InArgument<EntityReference> Configuration { get; set; }

    [Input("DynamicValues")]
    public InArgument<string> DynamicValues { get; set; }

    protected override void Execute(CodeActivityContext context)
    {
        var workflowContext = context.GetExtension<IWorkflowContext>();
        var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
        var service = serviceFactory.CreateOrganizationService(workflowContext.UserId);

        int recordsCreated = 0;
        int recordsErrored = 0;
        int totalRecordsProcessed = 0;
        System.Text.StringBuilder errorLog = new System.Text.StringBuilder();

        try
        {
            string fetchXML = FetchXMLInput.Get(context);
            EntityReference configRef = Configuration.Get(context);
            string dynamicValuesString = DynamicValues.Get(context) ?? "";

            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty");
                return;
            }

            Entity config = service.Retrieve("dsa_bulkrecordcreationconfiguration",
                configRef.Id,
                new ColumnSet("dsa_sourceentity", "dsa_targetentity", "dsa_attributesmapping", "dsa_referenceattribute"));

            if (config == null)
            {
                SetReturnValues(context, false, "Configuration record not found");
                return;
            }

            string sourceEntityName = config.GetAttributeValue<string>("dsa_sourceentity");
            string targetEntityName = config.GetAttributeValue<string>("dsa_targetentity");
            string attributesMappingString = config.GetAttributeValue<string>("dsa_attributesmapping");
            string referenceAttribute = config.GetAttributeValue<string>("dsa_referenceattribute");

            if (string.IsNullOrWhiteSpace(sourceEntityName) || string.IsNullOrWhiteSpace(targetEntityName))
            {
                SetReturnValues(context, false, "Source or target entity name is missing in configuration");
                return;
            }

            // Parse dynamic values
            Dictionary<string, string> dynamicValuesDict = ParseDynamicValues(dynamicValuesString);

            // Parse attribute mappings
            Dictionary<string, MappingDefinition> attributesMappingDict;
            try
            {
                attributesMappingDict = ParseAttributesMapping(attributesMappingString);

                if (!attributesMappingDict.Any())
                {
                    SetReturnValues(context, false, "No attribute mappings found in configuration");
                    return;
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Failed to parse attributes mapping: {ex.Message}");
                return;
            }

            QueryExpression query;
            try
            {
                var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest
                {
                    FetchXml = fetchXML
                };
                var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
                query = fetchToQueryResponse.Query;

                if (query == null)
                {
                    SetReturnValues(context, false, "Failed to convert FetchXML to QueryExpression");
                    return;
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Failed to parse FetchXML: {ex.Message}");
                return;
            }

            DateTime startTime = DateTime.Now;

            service.ProcessQuery<Entity>(query, sourceRecord =>
            {
                try
                {
                    Entity targetRecord = new Entity(targetEntityName);

                    foreach (var mapping in attributesMappingDict)
                    {
                        string targetField = mapping.Key;
                        MappingDefinition mappingDef = mapping.Value;

                        if (mappingDef.MappingType == MappingType.SourceAttribute)
                        {
                            // Source attribute mapping - original logic
                            string sourceField = mappingDef.Value;
                            if (sourceRecord.Contains(sourceField))
                            {
                                object sourceValue = sourceRecord[sourceField];
                                SetTargetFieldValue(targetRecord, targetField, sourceValue);
                            }
                        }
                        else if (mappingDef.MappingType == MappingType.StaticValue)
                        {
                            // Static value mapping
                            targetRecord[targetField] = mappingDef.Value;
                        }
                        else if (mappingDef.MappingType == MappingType.DynamicValue)
                        {
                            // Dynamic value mapping
                            string dynamicValueKey = mappingDef.Value.Substring(2, mappingDef.Value.Length - 3); // Remove ${ and }
                            if (dynamicValuesDict.TryGetValue(dynamicValueKey, out string dynamicValue))
                            {
                                targetRecord[targetField] = dynamicValue;
                            }
                            else
                            {
                                errorLog.AppendLine($"Dynamic value {dynamicValueKey} not found in inputs");
                            }
                        }
                        else if (mappingDef.MappingType == MappingType.ComplexValue)
                        {
                            // Complex value with concatenation and variable substitution
                            string processedValue = ProcessComplexValue(mappingDef.Value, dynamicValuesDict, sourceRecord);
                            targetRecord[targetField] = processedValue;
                        }
                    }

                    if (!string.IsNullOrEmpty(referenceAttribute))
                    {
                        targetRecord[referenceAttribute] = new EntityReference(
                            workflowContext.PrimaryEntityName,
                            workflowContext.PrimaryEntityId
                        );
                    }

                    service.Create(targetRecord);
                    recordsCreated++;
                }
                catch (Exception ex)
                {
                    errorLog.AppendLine($"Record {sourceRecord.Id}: Creation failed - {ex.Message}");
                    recordsErrored++;
                }

                totalRecordsProcessed++;
            });

            TimeSpan duration = DateTime.Now - startTime;
            string resultMessage = $"Process completed in {duration.TotalMinutes:F1} minutes. " +
                                 $"Created: {recordsCreated}, Errors: {recordsErrored}, Total Processed: {totalRecordsProcessed}";

            if (recordsErrored > 0)
            {
                resultMessage += $"\n\nError details:\n{errorLog}";
            }

            SetReturnValues(context, recordsErrored == 0, resultMessage);
        }
        catch (Exception ex)
        {
            SetReturnValues(context, false, $"Error: {ex.Message}");
        }
    }

    private void SetTargetFieldValue(Entity targetRecord, string targetField, object sourceValue)
    {
        // Handle different types of fields
        if (sourceValue is EntityReference sourceRef)
        {
            // If source is already an EntityReference, use it directly
            targetRecord[targetField] = sourceRef;
        }
        else if (sourceValue is AliasedValue aliasedValue)
        {
            // Handle aliased values (from linked entities)
            if (aliasedValue.Value is EntityReference aliasedRef)
            {
                targetRecord[targetField] = aliasedRef;
            }
            else if (aliasedValue.Value is Guid aliasedGuid)
            {
                targetRecord[targetField] = new EntityReference(aliasedValue.EntityLogicalName, aliasedGuid);
            }
            else
            {
                targetRecord[targetField] = aliasedValue.Value;
            }
        }
        else if (sourceValue is Guid sourceGuid)
        {
            // For direct Guid values, create EntityReference using the account entity type
            // since we know this is an account lookup
            targetRecord[targetField] = new EntityReference("account", sourceGuid);
        }
        else if (sourceValue is Microsoft.Xrm.Sdk.Money moneyValue)
        {
            targetRecord[targetField] = moneyValue;
        }
        else if (sourceValue is OptionSetValue optionSetValue)
        {
            targetRecord[targetField] = optionSetValue;
        }
        else
        {
            // For non-lookup fields, copy value directly
            targetRecord[targetField] = sourceValue;
        }
    }

    private string ProcessComplexValue(string complexValue, Dictionary<string, string> dynamicValues, Entity sourceRecord)
    {
        // Process a complex value with dynamic variables and concatenation
        string result = complexValue;

        // Replace dynamic variables ${var} with their values
        Regex dynamicVarRegex = new Regex(@"\$\{([^}]+)\}");
        result = dynamicVarRegex.Replace(result, match =>
        {
            string varName = match.Groups[1].Value;
            if (dynamicValues.TryGetValue(varName, out string value))
            {
                return value;
            }
            return match.Value; // Keep original if not found
        });

        // Replace source field references with actual values
        Regex sourceFieldRegex = new Regex(@"\$\[([^]]+)\]");
        result = sourceFieldRegex.Replace(result, match =>
        {
            string fieldName = match.Groups[1].Value;
            if (sourceRecord.Contains(fieldName))
            {
                object value = sourceRecord[fieldName];
                if (value is AliasedValue av)
                {
                    return av.Value?.ToString() ?? "";
                }
                return value?.ToString() ?? "";
            }
            return match.Value; // Keep original if not found
        });

        return result;
    }

    private enum MappingType
    {
        SourceAttribute,  // Regular attribute mapping (original behavior)
        StaticValue,      // Static value with $"value" syntax
        DynamicValue,     // Dynamic value with ${variable} syntax
        ComplexValue      // Complex value with concatenation and mixed variables
    }

    private class MappingDefinition
    {
        public MappingType MappingType { get; set; }
        public string Value { get; set; }
    }

    private Dictionary<string, MappingDefinition> ParseAttributesMapping(string mappingString)
    {
        var mapping = new Dictionary<string, MappingDefinition>();
        if (string.IsNullOrWhiteSpace(mappingString))
        {
            return mapping;
        }

        var pairs = mappingString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var fields = pair.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 2)
            {
                string targetField = fields[0].Trim();
                string valueDefinition = fields[1].Trim();

                MappingDefinition mappingDef = new MappingDefinition();

                // Check if it's a static value with $"value" syntax
                if (valueDefinition.StartsWith("$\"") && valueDefinition.EndsWith("\""))
                {
                    mappingDef.MappingType = MappingType.StaticValue;
                    mappingDef.Value = valueDefinition.Substring(2, valueDefinition.Length - 3); // Remove $" and "
                }
                // Check if it's a dynamic value with ${variable} syntax
                else if (valueDefinition.StartsWith("${") && valueDefinition.EndsWith("}"))
                {
                    mappingDef.MappingType = MappingType.DynamicValue;
                    mappingDef.Value = valueDefinition;
                }
                // Check if it's a complex value with concatenation (contains ${var} or $"text")
                else if (valueDefinition.Contains("${") || valueDefinition.Contains("$\"") || valueDefinition.Contains("$["))
                {
                    mappingDef.MappingType = MappingType.ComplexValue;
                    mappingDef.Value = valueDefinition;
                }
                // Otherwise, it's a regular attribute mapping
                else
                {
                    mappingDef.MappingType = MappingType.SourceAttribute;
                    mappingDef.Value = valueDefinition;
                }

                mapping[targetField] = mappingDef;
            }
        }

        return mapping;
    }

    private Dictionary<string, string> ParseDynamicValues(string dynamicValuesString)
    {
        var values = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(dynamicValuesString))
        {
            return values;
        }

        var pairs = dynamicValuesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split(new[] { '=' }, 2, StringSplitOptions.None);
            if (keyValue.Length == 2)
            {
                values[keyValue[0].Trim()] = keyValue[1];
            }
        }

        return values;
    }

    private void SetReturnValues(CodeActivityContext context, bool state, string text)
    {
        ReturnState.Set(context, state);
        ReturnText.Set(context, text);
    }
}