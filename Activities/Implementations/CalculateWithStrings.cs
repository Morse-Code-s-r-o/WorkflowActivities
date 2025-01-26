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
    public class CalculateTwoNumbers : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("String A")]
        public InArgument<string> StringA { get; set; }

        [RequiredArgument]
        [Input("String B")]
        public InArgument<string> StringB { get; set; }

        [RequiredArgument]
        [Input("Operator")]
        public InArgument<string> Operator { get; set; }

        [Output("Result")]
        public OutArgument<decimal> Result { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            var (inputA, inputB) = GetAndValidateInputs(executionContext);
            if (inputA == null || inputB == null) return;

            string operation = Operator.Get(executionContext);
            if (!TryCalculate(inputA.Value, inputB.Value, operation, out decimal result, out string error))
            {
                SetReturnValues(executionContext, false, error);
                Result.Set(executionContext, 0);
                return;
            }

            Result.Set(executionContext, result);
            SetReturnValues(executionContext, true, "OK");
        }

        private (decimal? a, decimal? b) GetAndValidateInputs(CodeActivityContext context)
        {
            string inputA = StringA.Get(context) ?? "0";
            string inputB = StringB.Get(context) ?? "0";

            if (!decimal.TryParse(inputA, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal a))
            {
                SetReturnValues(context, false, $"String A is not a valid decimal: '{inputA}'");
                Result.Set(context, 0);
                return (null, null);
            }

            if (!decimal.TryParse(inputB, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal b))
            {
                SetReturnValues(context, false, $"String B is not a valid decimal: '{inputB}'");
                Result.Set(context, 0);
                return (null, null);
            }

            return (a, b);
        }

        private bool TryCalculate(decimal a, decimal b, string operation, out decimal result, out string error)
        {
            result = 0;
            error = string.Empty;

            switch (operation)
            {
                case "+":
                    result = a + b;
                    return true;
                case "-":
                    result = a - b;
                    return true;
                case "*":
                    result = a * b;
                    return true;
                case "/":
                    if (b == 0m)
                    {
                        error = "Division by zero is not allowed.";
                        return false;
                    }
                    result = a / b;
                    return true;
                default:
                    error = "Invalid operator. Use +, -, * or /.";
                    return false;
            }
        }
    }
}