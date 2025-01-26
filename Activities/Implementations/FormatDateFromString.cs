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
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using System.Globalization;

namespace WorkflowActivities.Implementations
{
    public class FormatDateFromString : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("Date")]
        public InArgument<string> Date { get; set; }

        [RequiredArgument]
        [Input("Input Format")]
        public InArgument<string> InputFormat { get; set; }

        [RequiredArgument]
        [Input("Result Format")]
        public InArgument<string> ResultFormat { get; set; }

        [Output("Formatted Date")]
        public OutArgument<string> FormattedDate { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            string dateStr = Date.Get(executionContext);
            string inputFormat = InputFormat.Get(executionContext);
            string resultFormat = ResultFormat.Get(executionContext);

            if (!DateTime.TryParseExact(dateStr, inputFormat, CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out DateTime parsedDate))
            {
                FormattedDate.Set(executionContext, dateStr);
                SetReturnValues(executionContext, false, 
                    $"Date '{dateStr}' is not valid according to the format '{inputFormat}'.");
                return;
            }

            string formattedDate = parsedDate.ToString(resultFormat, CultureInfo.InvariantCulture);
            FormattedDate.Set(executionContext, formattedDate);
            SetReturnValues(executionContext, true, "Date formatted successfully.");
        }
    }
}