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
using Microsoft.Xrm.Sdk.Workflow;
using WorkflowActivities.Base;
using System.Text.RegularExpressions;

namespace WorkflowActivities.Implementations
{
    public class ReplaceRegex : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("InputString")]
        public InArgument<string> InputString { get; set; }

        [RequiredArgument]
        [Input("Regex Pattern")]
        public InArgument<string> RegexPattern { get; set; }

        [RequiredArgument]
        [Input("Replacement Value")]
        public InArgument<string> ReplacementValue { get; set; }

        [Output("Output String")]
        public OutArgument<string> OutputString { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            string inputString = InputString.Get(executionContext);
            string regexPattern = RegexPattern.Get(executionContext);
            string replacementValue = ReplacementValue.Get(executionContext);

            try
            {
                Regex regex = new Regex(regexPattern);
                string result = regex.Replace(inputString, replacementValue);

                OutputString.Set(executionContext, result);
                SetReturnValues(executionContext, true, "Replacement completed successfully.");
            }
            catch (ArgumentException ex)
            {
                OutputString.Set(executionContext, inputString);
                SetReturnValues(executionContext, false, "Regex pattern error: " + ex.Message);
            }
            catch (Exception ex)
            {
                OutputString.Set(executionContext, inputString);
                SetReturnValues(executionContext, false, "Error: " + ex.Message);
            }
        }
    }
}