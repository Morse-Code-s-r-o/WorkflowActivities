/*
 * ----------------------------------------------------------------------------
 * "Workflow Activities" - A project by Morse & Code s.r.o.
 * ----------------------------------------------------------------------------
 * Copyright (c) 2025 Morse & Code s.r.o. All rights reserved.
 *
 * Licensed under the MIT License (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     https://opensource.org/licenses/MIT
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * You must provide proper attribution when using this code, including this
 * notice, in any copies or substantial portions of the Software.
 *
 * Commercial use is allowed, but all rights remain with Morse & Code s.r.o.
 * ----------------------------------------------------------------------------
 */

using System;
using System.Activities;
using System.Linq;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;

namespace WorkflowActivities.Implementations
{
    public class UpdateRecordsActivity : PagedWorkflowActivityBase
    {
        [Input("UpdateAttribute")]
        [RequiredArgument]
        public InArgument<string> UpdateAttribute { get; set; }

        [Input("UpdateValue")]
        public InArgument<string> UpdateValue { get; set; }

        public UpdateRecordsActivity() : base(new PagedQueryProcessor()) { }

        public UpdateRecordsActivity(IPagedQueryProcessor queryProcessor = null) : base(queryProcessor) { }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var (service, workflowContext) = GetServices(context);
            var (fetchXML, updateAttribute, updateValue) = GetInputs(context);

            var entityName = GetEntityNameFromFetchXML(fetchXML);
            var entityMetadata = GetEntityMetadata(service, entityName);
            var attributeMetadata = GetAttributeMetadata(entityMetadata, updateAttribute);

            var query = CreateQueryExpression(service, fetchXML);
            ProcessRecordUpdates(service, query, context, updateAttribute, updateValue, attributeMetadata);
        }

        private (IOrganizationService service, IWorkflowContext workflowContext) GetServices(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            return (serviceFactory.CreateOrganizationService(workflowContext.UserId), workflowContext);
        }

        private (string fetchXML, string updateAttribute, string updateValue) GetInputs(CodeActivityContext context)
        {
            var fetchXML = FetchXMLInput.Get(context);
            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty");
                return (null, null, null);
            }

            var updateAttribute = UpdateAttribute.Get(context);
            if (string.IsNullOrWhiteSpace(updateAttribute))
            {
                SetReturnValues(context, false, "UpdateAttribute cannot be empty");
                return (null, null, null);
            }

            return (fetchXML, updateAttribute, UpdateValue.Get(context));
        }

        private string GetEntityNameFromFetchXML(string fetchXML)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(fetchXML);
                var entityElement = doc.Descendants("entity").FirstOrDefault();
                if (entityElement?.Attribute("name") == null)
                {
                    return null;
                }
                return entityElement.Attribute("name").Value;
            }
            catch
            {
                return null;
            }
        }

        private EntityMetadata GetEntityMetadata(IOrganizationService service, string entityLogicalName)
        {
            var request = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = entityLogicalName
            };

            var response = (RetrieveEntityResponse)service.Execute(request);
            return response.EntityMetadata;
        }

        private AttributeMetadata GetAttributeMetadata(EntityMetadata entityMetadata, string attributeName)
        {
            return entityMetadata.Attributes.FirstOrDefault(a => a.LogicalName == attributeName);
        }

        private QueryExpression CreateQueryExpression(IOrganizationService service, string fetchXML)
        {
            var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest { FetchXml = fetchXML };
            var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
            return fetchToQueryResponse.Query;
        }

        private void ProcessRecordUpdates(IOrganizationService service, QueryExpression query, 
            CodeActivityContext context, string updateAttribute, string updateValue, AttributeMetadata attributeMetadata)
        {
            int recordsUpdated = 0;
            int recordsErrored = 0;
            int totalRecordsProcessed = 0;
            var errorLog = new System.Text.StringBuilder();
            DateTime startTime = DateTime.Now;

            var valueConverter = new AttributeValueConverter(attributeMetadata);

            _queryProcessor.ProcessPagedQuery<Entity>(service, query, record =>
            {
                try
                {
                    Entity updateRecord = new Entity(record.LogicalName, record.Id);
                    try 
                    {
                        updateRecord[updateAttribute] = valueConverter.ConvertValue(updateValue, record);
                        service.Update(updateRecord);
                        recordsUpdated++;
                    }
                    catch (Exception ex)
                    {
                        errorLog.AppendLine($"Record {record.Id}: {ex.Message}");
                        recordsErrored++;
                    }
                }
                catch (Exception ex)
                {
                    errorLog.AppendLine($"Record {record.Id}: General error - {ex.Message}");
                    recordsErrored++;
                }

                totalRecordsProcessed++;
            });

            GenerateResults(context, startTime, recordsUpdated, recordsErrored, totalRecordsProcessed, errorLog.ToString());
        }

        private void GenerateResults(CodeActivityContext context, DateTime startTime, 
            int recordsUpdated, int recordsErrored, int totalRecordsProcessed, string errorLog)
        {
            TimeSpan duration = DateTime.Now - startTime;
            string resultMessage = $"Process completed in {duration.TotalMinutes:F1} minutes. " +
                                 $"Updated: {recordsUpdated}, Errors: {recordsErrored}, Total Processed: {totalRecordsProcessed}";

            if (recordsErrored > 0)
            {
                resultMessage += $"\n\nError details:\n{errorLog}";
            }
            
            SetReturnValues(context, recordsErrored == 0, resultMessage);
        }
    }

    public class AttributeValueConverter
    {
        private readonly AttributeMetadata _attributeMetadata;
        private readonly bool _isLookup;
        private readonly string _targetEntity;

        public AttributeValueConverter(AttributeMetadata attributeMetadata)
        {
            _attributeMetadata = attributeMetadata;
            _isLookup = attributeMetadata is LookupAttributeMetadata;
            if (_isLookup)
            {
                var lookupMetadata = (LookupAttributeMetadata)attributeMetadata;
                _targetEntity = lookupMetadata.Targets?[0];
            }
        }

        public object ConvertValue(string value, Entity currentRecord)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (_isLookup)
            {
                return ConvertLookupValue(value, currentRecord);
            }

            return ConvertNonLookupValue(value);
        }

        private EntityReference ConvertLookupValue(string value, Entity currentRecord)
        {
            if (!Guid.TryParse(value, out Guid lookupId))
            {
                return null;
            }

            if (currentRecord.LogicalName == _targetEntity && currentRecord.Id == lookupId)
            {
                return null;
            }

            return new EntityReference(_targetEntity, lookupId);
        }

        private object ConvertNonLookupValue(string value)
        {
            try
            {
                return _attributeMetadata.AttributeType switch
                {
                    AttributeTypeCode.Boolean => bool.TryParse(value, out bool boolValue) ? boolValue : null,
                    AttributeTypeCode.DateTime => DateTime.TryParse(value, out DateTime dateValue) ? dateValue : null,
                    AttributeTypeCode.Decimal => decimal.TryParse(value, out decimal decimalValue) ? decimalValue : null,
                    AttributeTypeCode.Double => double.TryParse(value, out double doubleValue) ? doubleValue : null,
                    AttributeTypeCode.Integer => int.TryParse(value, out int intValue) ? intValue : null,
                    AttributeTypeCode.Money => decimal.TryParse(value, out decimal moneyValue) ? new Money(moneyValue) : null,
                    AttributeTypeCode.Picklist or AttributeTypeCode.State or AttributeTypeCode.Status => 
                        ConvertOptionSet(value),
                    AttributeTypeCode.String or AttributeTypeCode.Memo => value,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private OptionSetValue ConvertOptionSet(string value)
        {
            if (!int.TryParse(value, out int optionValue))
            {
                return null;
            }

            var picklistMetadata = (EnumAttributeMetadata)_attributeMetadata;
            var validOptions = picklistMetadata.OptionSet.Options.Select(o => o.Value.Value).ToList();
            
            return validOptions.Contains(optionValue) ? new OptionSetValue(optionValue) : null;
        }
    }
}