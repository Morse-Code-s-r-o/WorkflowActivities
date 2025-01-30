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
using System.ServiceModel;
using System.Activities;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;

namespace WorkflowActivities.Implementations
{
    public class UpdateRelationshipActivity : PagedWorkflowActivityBase
    {
        [Input("Relationship Name")]
        [RequiredArgument]
        public InArgument<string> RelationshipName { get; set; }

        [Input("Append")]
        [RequiredArgument]
        public InArgument<bool> AppendIsRequired { get; set; }

        public UpdateRelationshipActivity() : base(new PagedQueryProcessor()) { }

        public UpdateRelationshipActivity(IPagedQueryProcessor queryProcessor = null) : base(queryProcessor) { }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var (service, workflowContext) = GetServices(context);
            var fetchXML = FetchXMLInput.Get(context);
            var relationshipName = RelationshipName.Get(context);
            var appendIsRequired = AppendIsRequired.Get(context);

            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(relationshipName))
            {
                SetReturnValues(context, false, "Relationship Name cannot be empty.");
                return;
            }

            var query = CreateQueryExpression(service, fetchXML);

            if (query == null || query.EntityName == null)
            {
                SetReturnValues(context, false, "Failed to parse FetchXML or determine entity name.");
                return;
            }

            var targetEntityReference = new EntityReference(workflowContext.PrimaryEntityName, workflowContext.PrimaryEntityId);
            int totalProcessed = 0;
            int errors = 0;

            var errorLog = new System.Text.StringBuilder();

            _queryProcessor.ProcessPagedQuery<Entity>(service, query, relatedEntity =>
            {
                try
                {
                    if (appendIsRequired)
                    {
                        var request = new AssociateRequest
                        {
                            Target = targetEntityReference,
                            RelatedEntities = new EntityReferenceCollection { new EntityReference(relatedEntity.LogicalName, relatedEntity.Id) },
                            Relationship = new Relationship(relationshipName)
                        };
                        service.Execute(request);
                    }
                    else
                    {
                        var request = new DisassociateRequest
                        {
                            Target = targetEntityReference,
                            RelatedEntities = new EntityReferenceCollection { new EntityReference(relatedEntity.LogicalName, relatedEntity.Id) },
                            Relationship = new Relationship(relationshipName)
                        };
                        service.Execute(request);
                    }
                    totalProcessed++;
                }
                catch (Exception ex)
                {
                    var detailedError = $"Error details:\n" +
                        $"Target: {targetEntityReference.LogicalName}({targetEntityReference.Id})\n" +
                        $"Related: {relatedEntity.LogicalName}({relatedEntity.Id})\n" +
                        $"Relationship: {relationshipName}\n" +
                        $"Error: {(ex is FaultException<OrganizationServiceFault> fault ? fault.Detail.Message : ex.Message)}";
                    
                    errorLog.AppendLine(detailedError);
                    errors++;
                }
            });

            GenerateResults(context, totalProcessed, errors, errorLog.ToString());
        }

        private (IOrganizationService service, IWorkflowContext workflowContext) GetServices(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            return (serviceFactory.CreateOrganizationService(workflowContext.UserId), workflowContext);
        }

        private QueryExpression CreateQueryExpression(IOrganizationService service, string fetchXML)
        {
            try
            {
                var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest { FetchXml = fetchXML };
                var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
                return fetchToQueryResponse.Query;
            }
            catch
            {
                return null;
            }
        }

        private void GenerateResults(CodeActivityContext context, int totalProcessed, int errors, string errorLog)
        {
            string resultMessage = $"Processed {totalProcessed} records with {errors} errors.";
            if (errors > 0)
            {
                resultMessage += $"\nError Log:\n{errorLog}";
            }
            SetReturnValues(context, errors == 0, resultMessage);
        }
    }
}
