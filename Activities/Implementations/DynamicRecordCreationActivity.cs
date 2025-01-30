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
using System.Linq;
using System.Activities;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using System.Xml.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;

namespace WorkflowActivities.Implementations
{
    public class DynamicRecordCreationActivity : PagedWorkflowActivityBase
    {
        [Input("Mapping Web Resource")]
        [ReferenceTarget("webresource")]
        [RequiredArgument]
        public InArgument<EntityReference> WebResource { get; set; }

        [Input("ReferenceIdField")]
        public InArgument<string> ReferenceIdField { get; set; }

        public DynamicRecordCreationActivity() : base(new PagedQueryProcessor()) { }
        
        public DynamicRecordCreationActivity(IPagedQueryProcessor queryProcessor = null) : base(queryProcessor) { }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var (service, workflowContext) = GetServices(context);
            var (fetchXML, webResourceRef, referenceIdField) = GetInputs(context);

            string webResourceXML = GetWebResourceContent(service, webResourceRef, context);
            if (webResourceXML == null) return;

            if (!ValidateXMLInputs(webResourceXML, out XDocument xmlDoc, out string errorMessage))
            {
                SetReturnValues(context, false, errorMessage);
                return;
            }

            var mappingRoot = xmlDoc.Element("mapping");
            var targetEntityName = mappingRoot.Element("targetEntity")?.Element("name")?.Value;
            
            if (string.IsNullOrWhiteSpace(targetEntityName))
            {
                SetReturnValues(context, false, "Target entity name is missing or invalid");
                return;
            }

            var query = CreateQueryExpression(service, fetchXML);
            ProcessRecords(service, query, context, workflowContext, targetEntityName, mappingRoot, referenceIdField);
        }

        private (IOrganizationService service, IWorkflowContext workflowContext) GetServices(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            return (serviceFactory.CreateOrganizationService(workflowContext.UserId), workflowContext);
        }

        private (string fetchXML, EntityReference webResourceRef, string referenceIdField) GetInputs(CodeActivityContext context)
        {
            return (
                FetchXMLInput.Get(context),
                WebResource.Get(context),
                ReferenceIdField.Get(context)
            );
        }

        private string GetWebResourceContent(IOrganizationService service, EntityReference webResourceRef, CodeActivityContext context)
        {
            try
            {
                var webResource = service.Retrieve("webresource", webResourceRef.Id, new ColumnSet("content"));
                string base64Content = webResource?.GetAttributeValue<string>("content");

                if (string.IsNullOrEmpty(base64Content))
                {
                    SetReturnValues(context, false, "Web resource content is empty");
                    return null;
                }

                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Failed to retrieve web resource content: {ex.Message}");
                return null;
            }
        }

        private bool ValidateXMLInputs(string webResourceXML, out XDocument xmlDoc, out string errorMessage)
        {
            xmlDoc = null;
            errorMessage = string.Empty;

            try
            {
                xmlDoc = XDocument.Parse(webResourceXML);
                var mappingRoot = xmlDoc.Element("mapping");
                var sourceEntity = mappingRoot?.Element("sourceEntity");
                var targetEntity = mappingRoot?.Element("targetEntity");

                if (mappingRoot == null)
                {
                    errorMessage = "Invalid WebResourceXML format: missing root 'mapping' element";
                    return false;
                }

                if (sourceEntity == null || targetEntity == null)
                {
                    errorMessage = "Invalid WebResourceXML format: missing sourceEntity or targetEntity";
                    return false;
                }

                if (sourceEntity.Element("name") == null || targetEntity.Element("name") == null)
                {
                    errorMessage = "Missing required 'name' element in sourceEntity or targetEntity";
                    return false;
                }

                if (!targetEntity.Elements("fieldMapping").Any())
                {
                    errorMessage = "No field mappings found in targetEntity";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"XML Validation Error: {ex.Message}";
                return false;
            }
        }

        private QueryExpression CreateQueryExpression(IOrganizationService service, string fetchXML)
        {
            var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest { FetchXml = fetchXML };
            var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
            return fetchToQueryResponse.Query;
        }

        private void ProcessRecords(IOrganizationService service, QueryExpression query, CodeActivityContext context,
            IWorkflowContext workflowContext, string targetEntityName, XElement mappingRoot, string referenceIdField)
        {
            int recordsCreated = 0;
            int recordsErrored = 0;
            int totalRecordsProcessed = 0;
            var errorLog = new System.Text.StringBuilder();

            _queryProcessor.ProcessPagedQuery<Entity>(service, query, sourceRecord =>
            {
                try
                {
                    Entity targetRecord = new Entity(targetEntityName);
                    MapFields(mappingRoot.Element("sourceEntity"), mappingRoot.Element("targetEntity"), 
                        sourceRecord, targetRecord);

                    if (!string.IsNullOrEmpty(referenceIdField))
                    {
                        targetRecord[referenceIdField] = new EntityReference(
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

            GenerateResults(context, recordsCreated, recordsErrored, totalRecordsProcessed, errorLog.ToString());
        }

        private void GenerateResults(CodeActivityContext context,
            int recordsCreated, int recordsErrored, int totalRecordsProcessed, string errorLog)
        {
            string resultMessage = $"Process completed. " +
                                 $"Created: {recordsCreated}, Errors: {recordsErrored}, Total Processed: {totalRecordsProcessed}";

            if (recordsErrored > 0)
            {
                resultMessage += $"\n\nError details:\n{errorLog}";
            }

            SetReturnValues(context, recordsErrored == 0, resultMessage);
        }

        private void MapFields(XElement sourceEntity, XElement targetEntity, Entity sourceRecord, Entity targetRecord)
        {
            foreach (var mapping in targetEntity.Elements("fieldMapping"))
            {
                string sourceField = mapping.Element("sourceField")?.Value;
                string targetField = mapping.Element("targetField")?.Value;

                if (sourceRecord.Contains(sourceField))
                {
                    targetRecord[targetField] = sourceRecord[sourceField];
                }
            }
        }
    }
}