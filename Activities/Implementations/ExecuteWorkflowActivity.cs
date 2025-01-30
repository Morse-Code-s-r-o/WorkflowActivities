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
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;

namespace WorkflowActivities.Implementations
{
    public class ExecuteWorkflowActivity : PagedWorkflowActivityBase
    {
        [Input("Workflow")]
        [ReferenceTarget("workflow")]
        [RequiredArgument]
        public InArgument<EntityReference> Workflow { get; set; }

        public ExecuteWorkflowActivity() : base(new PagedQueryProcessor()) { }

        public ExecuteWorkflowActivity(IPagedQueryProcessor queryProcessor = null) : base(queryProcessor) { }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            var service = serviceFactory.CreateOrganizationService(workflowContext.UserId);

            string fetchXML = FetchXMLInput.Get(context);
            EntityReference workflow = Workflow.Get(context);

            if (!ValidateInputs(service, context, fetchXML, workflow))
            {
                return;
            }

            var query = CreateQueryExpression(service, fetchXML);
            DateTime startTime = DateTime.Now;
            ProcessRecords(service, query, workflow, context, startTime);
        }

        private bool ValidateInputs(IOrganizationService service, CodeActivityContext context, string fetchXML, EntityReference workflow)
        {
            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty");
                return false;
            }

            if (workflow == null)
            {
                SetReturnValues(context, false, "Workflow reference cannot be null");
                return false;
            }

            try
            {
                var columns = new ColumnSet("statecode", "name", "primaryentity");
                var workflowEntity = service.Retrieve(workflow.LogicalName, workflow.Id, columns);
                
                if (workflowEntity == null)
                {
                    SetReturnValues(context, false, "Workflow not found");
                    return false;
                }

                if (workflowEntity.GetAttributeValue<OptionSetValue>("statecode")?.Value != 1)
                {
                    SetReturnValues(context, false, $"Workflow '{workflowEntity.GetAttributeValue<string>("name")}' must be in Activated state");
                    return false;
                }

                var doc = System.Xml.Linq.XDocument.Parse(fetchXML);
                var entityName = doc.Descendants("entity").FirstOrDefault()?.Attribute("name")?.Value;
                string workflowEntity_PrimaryEntity = workflowEntity.GetAttributeValue<string>("primaryentity");

                if (!string.IsNullOrEmpty(workflowEntity_PrimaryEntity) && 
                    !string.IsNullOrEmpty(entityName) && 
                    !workflowEntity_PrimaryEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                {
                    SetReturnValues(context, false, $"Workflow primary entity ({workflowEntity_PrimaryEntity}) does not match FetchXML entity ({entityName})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Validation error: {ex.Message}");
                return false;
            }

            return true;
        }

        private QueryExpression CreateQueryExpression(IOrganizationService service, string fetchXML)
        {
            var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest { FetchXml = fetchXML };
            var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
            return fetchToQueryResponse.Query;
        }

        private void ProcessRecords(IOrganizationService service, QueryExpression query, 
            EntityReference workflow, CodeActivityContext context, DateTime startTime)
        {
            int recordsProcessed = 0;
            int recordsErrored = 0;
            int totalRecordsProcessed = 0;
            var errorLog = new System.Text.StringBuilder();

            _queryProcessor.ProcessPagedQuery<Entity>(service, query, record =>
            {
                try
                {
                    var executeWorkflowRequest = new ExecuteWorkflowRequest
                    {
                        WorkflowId = workflow.Id,
                        EntityId = record.Id
                    };

                    service.Execute(executeWorkflowRequest);
                    recordsProcessed++;
                }
                catch (Exception ex)
                {
                    errorLog.AppendLine($"Record {record.Id}: Failed to execute workflow - {ex.Message}");
                    recordsErrored++;
                }

                totalRecordsProcessed++;
            });

            TimeSpan duration = DateTime.Now - startTime;
            string resultMessage = $"Process completed in {duration.TotalMinutes:F1} minutes. " +
                                 $"Processed: {recordsProcessed}, Errors: {recordsErrored}, Total Records: {totalRecordsProcessed}";

            if (recordsErrored > 0)
            {
                resultMessage += $"\n\nError details:\n{errorLog}";
            }
            
            SetReturnValues(context, recordsErrored == 0, resultMessage);
        }
    }
}