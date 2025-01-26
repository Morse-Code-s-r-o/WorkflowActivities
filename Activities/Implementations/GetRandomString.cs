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

namespace WorkflowActivities.Implementations
{
    public class GetRandomString : WorkflowActivityBase
    {
        [RequiredArgument]
        [Input("Length of the Random String")]
        public InArgument<int> StringLength { get; set; }

        [Output("Random String")]
        public OutArgument<string> RandomString { get; set; }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            int length = StringLength.Get(executionContext);
            string result = GenerateRandomString(length);
            RandomString.Set(executionContext, result);
            SetReturnValues(executionContext, true, "Random string generated successfully");
        }

        private string GenerateRandomString(int length)
        {
            const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var charactersLength = characters.Length;
            var random = new Random();
            var result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = characters[random.Next(charactersLength)];
            }

            return new string(result);
        }
    }
}