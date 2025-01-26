using System;
using System.Activities;
using System.Linq;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Web;

namespace WorkflowActivities.Implementations
{
    public class GetRecordGuid : PagedWorkflowActivityBase
    {
        [Input("Dynamic URL")]
        public InArgument<string> DynamicUrl { get; set; }

        [Input("Lookup Field Name")]
        public InArgument<string> LookupName { get; set; }

        [Output("Record GUID")]
        public OutArgument<string> RecordGuid { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext context)
        {
            var workflowContext = context.GetExtension<IWorkflowContext>();
            var serviceFactory = context.GetExtension<IOrganizationServiceFactory>();
            var service = serviceFactory.CreateOrganizationService(workflowContext.UserId);

            string url = DynamicUrl.Get(context);
            string lookupName = LookupName.Get(context);
            string fetchXML = FetchXMLInput.Get(context);

            if (!string.IsNullOrWhiteSpace(url))
            {
                HandleUrlGuid(context, url);
                return;
            }

            if (!string.IsNullOrWhiteSpace(lookupName))
            {
                HandleLookupGuid(context, service, workflowContext, lookupName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(fetchXML))
            {
                HandleFetchXMLGuid(context, service, fetchXML);
                return;
            }

            HandleCurrentRecord(context, workflowContext);
        }

        private void HandleCurrentRecord(CodeActivityContext context, IWorkflowContext workflowContext)
        {
            string currentGuid = workflowContext.PrimaryEntityId.ToString();
            SetReturnValues(context, true, "Current record GUID retrieved");
            RecordGuid.Set(context, currentGuid);
        }

        private void HandleUrlGuid(CodeActivityContext context, string url)
        {
            try
            {
                Uri uri = new Uri(url);
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                string guid = queryParams["id"]?.Trim('{', '}');

                if (string.IsNullOrEmpty(guid))
                {
                    SetReturnValues(context, false, "No GUID found in URL");
                    RecordGuid.Set(context, string.Empty);
                    return;
                }

                if (Guid.TryParse(guid, out _))
                {
                    SetReturnValues(context, true, "GUID extracted from URL");
                    RecordGuid.Set(context, guid);
                }
                else
                {
                    SetReturnValues(context, false, "Invalid GUID format in URL");
                    RecordGuid.Set(context, string.Empty);
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Error processing URL: {ex.Message}");
                RecordGuid.Set(context, string.Empty);
            }
        }

        private void HandleLookupGuid(CodeActivityContext context, IOrganizationService service, 
            IWorkflowContext workflowContext, string lookupName)
        {
            try
            {
                Entity record = service.Retrieve(workflowContext.PrimaryEntityName, 
                    workflowContext.PrimaryEntityId, 
                    new ColumnSet(lookupName));

                if (record.Contains(lookupName) && record[lookupName] is EntityReference lookupRef)
                {
                    SetReturnValues(context, true, $"GUID retrieved from lookup field {lookupName}");
                    RecordGuid.Set(context, lookupRef.Id.ToString());
                }
                else
                {
                    SetReturnValues(context, false, $"Lookup field {lookupName} is empty or not a lookup type");
                    RecordGuid.Set(context, string.Empty);
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Error retrieving lookup field {lookupName}: {ex.Message}");
                RecordGuid.Set(context, string.Empty);
            }
        }

        private void HandleFetchXMLGuid(CodeActivityContext context, IOrganizationService service, string fetchXML)
        {
            if (string.IsNullOrWhiteSpace(fetchXML))
            {
                SetReturnValues(context, false, "FetchXML cannot be empty");
                RecordGuid.Set(context, string.Empty);
                return;
            }

            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(fetchXML);
                var entityElement = doc.Descendants("entity").FirstOrDefault();
                if (entityElement?.Attribute("name") == null)
                {
                    SetReturnValues(context, false, "Invalid FetchXML: Could not find entity element or name attribute");
                    RecordGuid.Set(context, string.Empty);
                    return;
                }

                var fetchToQueryRequest = new FetchXmlToQueryExpressionRequest { FetchXml = fetchXML };
                var fetchToQueryResponse = (FetchXmlToQueryExpressionResponse)service.Execute(fetchToQueryRequest);
                var query = fetchToQueryResponse.Query;
                query.TopCount = 1;

                var result = service.RetrieveMultiple(query);

                if (result.Entities.Count > 0)
                {
                    string guid = result.Entities[0].Id.ToString();
                    SetReturnValues(context, true, "GUID retrieved from FetchXML");
                    RecordGuid.Set(context, guid);
                }
                else
                {
                    SetReturnValues(context, false, "No records found with FetchXML");
                    RecordGuid.Set(context, string.Empty);
                }
            }
            catch (Exception ex)
            {
                SetReturnValues(context, false, $"Error processing FetchXML: {ex.Message}");
                RecordGuid.Set(context, string.Empty);
            }
        }
    }
}