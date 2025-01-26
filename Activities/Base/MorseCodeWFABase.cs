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
using System.ComponentModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.Linq;

namespace WorkflowActivities.Base
{
    public interface IPagedQueryProcessor
    {
        public void ProcessPagedQuery<T>(IOrganizationService service, QueryExpression query, Action<T> processAction, bool usePageingCookie = true) where T : Entity;
    }

    [Browsable(false)]
    public class PagedQueryProcessor : IPagedQueryProcessor
    {
        public void ProcessPagedQuery<T>(IOrganizationService service, Microsoft.Xrm.Sdk.Query.QueryExpression query, Action<T> processAction, bool usePageingCookie = true) where T : Entity
        {
            if (query.PageInfo == null)
            {
                query.PageInfo = new Microsoft.Xrm.Sdk.Query.PagingInfo { Count = 200, PageNumber = 1 };
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

    [Browsable(false)]
    public abstract class WorkflowActivityBase : CodeActivity
    {
        [Output("Result State")]
        public OutArgument<bool> ResultState { get; set; }

        [Output("Result Text")]
        public OutArgument<string> ResultText { get; set; }

        protected void SetReturnValues(CodeActivityContext context, bool state, string text)
        {
            ResultState.Set(context, state);
            ResultText.Set(context, text);
        }

        protected override void Execute(CodeActivityContext executionContext)
        {
            try
            {
                ExecuteWorkflowLogic(executionContext);
            }
            catch (Exception ex)
            {
                SetReturnValues(executionContext, false, $"Error: {ex.Message}. Inner exception: {ex.InnerException?.Message}");
            }
        }

        protected abstract void ExecuteWorkflowLogic(CodeActivityContext executionContext);
    }

    [Browsable(false)]
    public abstract class PagedWorkflowActivityBase : WorkflowActivityBase
    {
        protected readonly IPagedQueryProcessor _queryProcessor;

        protected PagedWorkflowActivityBase(IPagedQueryProcessor queryProcessor = null)
        {
            _queryProcessor = queryProcessor ?? new PagedQueryProcessor();
        }

        [Input("FetchXML")]
        [RequiredArgument]
        public InArgument<string> FetchXMLInput { get; set; }
    }
}