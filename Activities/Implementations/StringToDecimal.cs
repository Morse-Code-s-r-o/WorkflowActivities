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
using System.Globalization;

namespace WorkflowActivities.Implementations
{
    public class StringToDecimal : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("String A")]
        public InArgument<string> StringA { get; set; }

        [Output("Result")]
        public OutArgument<decimal> Result { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            string inputA = StringA.Get(executionContext) ?? "0";

            if (!decimal.TryParse(inputA, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal a))
            {
                SetReturnValues(executionContext, false, $"String A is not a valid decimal: '{inputA}'");
                Result.Set(executionContext, 0);
                return;
            }
            
            Result.Set(executionContext, a);
            SetReturnValues(executionContext, true, "OK");
        }
    }
}