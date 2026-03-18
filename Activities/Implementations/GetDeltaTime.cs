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

namespace WorkflowActivities.Implementations
{
    public class GetDeltaTime : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("Time A")]
        public InArgument<DateTime> TimeA { get; set; }

        [RequiredArgument]
        [Input("Time B")]
        public InArgument<DateTime> TimeB { get; set; }

        [RequiredArgument]
        [Input("Return")]
        public InArgument<string> ReturnType { get; set; }

        [Output("DeltaTime")]
        public OutArgument<decimal> DeltaTime { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            var timeA = TimeA.Get(executionContext);
            var timeB = TimeB.Get(executionContext);
            var returnType = ReturnType.Get(executionContext);

            var delta = (timeA - timeB).Duration();

            if (!TryGetDeltaValue(delta, returnType, out var deltaValue))
            {
                DeltaTime.Set(executionContext, 0m);
                SetReturnValues(executionContext, false, "Invalid Return value. Use Days, Hours, Minutes, or Seconds.");
                return;
            }

            DeltaTime.Set(executionContext, deltaValue);
            SetReturnValues(executionContext, true, "OK");
        }

        private bool TryGetDeltaValue(TimeSpan delta, string returnType, out decimal value)
        {
            value = 0m;

            switch ((returnType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "days":
                    value = (decimal)delta.TotalDays;
                    return true;
                case "hours":
                    value = (decimal)delta.TotalHours;
                    return true;
                case "minutes":
                    value = (decimal)delta.TotalMinutes;
                    return true;
                case "seconds":
                    value = (decimal)delta.TotalSeconds;
                    return true;
                default:
                    return false;
            }
        }
    }
}
