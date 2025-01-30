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
using System.ComponentModel;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Metadata;
using System.Configuration;

namespace WorkflowActivities.Implementations
{
    [SettingsGroupName("Data")]
    [Description("Counts Records Based on FetchXML")]
    public class CountRecordsActivity : PagedWorkflowActivityBase
    {
        [Output("RecordCount")]
        public OutArgument<int> RecordCount { get; set; }

        public CountRecordsActivity() : base(new PagedQueryProcessor()) { }

        public CountRecordsActivity(IPagedQueryProcessor queryProcessor = null) : base(queryProcessor) { }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var (service, workflowContext) = GetServices(context);
            string fetchXML = FetchXMLInput.Get(context);

            if (!ValidateFetchXML(fetchXML, context))
            {
                return;
            }

            var query = CreateQueryExpression(service, fetchXML);
            ProcessCount(service, query, context);
        }

        private (IOrganizationService service, IWorkflowContext workflowContext) GetServices(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            return (serviceFactory.CreateOrganizationService(workflowContext.UserId), workflowContext);
        }

        private bool ValidateFetchXML(string fetchXML, CodeActivityContext context)
        {
            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty");
                RecordCount.Set(context, -1);
                return false;
            }

            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(fetchXML);
                var entityElement = doc.Descendants("entity").FirstOrDefault();
                if (entityElement?.Attribute("name") == null)
                {
                    SetReturnValues(context, false, "Invalid FetchXML: Could not find entity element or name attribute");
                    RecordCount.Set(context, -1);
                    return false;
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Invalid FetchXML format: {ex.Message}");
                RecordCount.Set(context, -1);
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

        private void ProcessCount(IOrganizationService service, QueryExpression query, CodeActivityContext context)
        {
            int totalRecords = 0;

            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                ReturnTotalRecordCount = true
            };

            try
            {
                EntityCollection result;
                do
                {
                    result = service.RetrieveMultiple(query);
                    totalRecords += result.Entities.Count;

                    query.PageInfo.PageNumber++;
                    query.PageInfo.PagingCookie = result.PagingCookie;

                } while (result.MoreRecords);

                string resultMessage = $"Count completed. Total Records: {totalRecords:N0}";
                
                SetReturnValues(context, true, resultMessage);
                RecordCount.Set(context, totalRecords);
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Error retrieving records: {ex.Message}");
                RecordCount.Set(context, -1);
            }
        }
    }
}