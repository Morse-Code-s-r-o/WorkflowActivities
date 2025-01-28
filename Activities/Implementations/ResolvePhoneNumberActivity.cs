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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using WorkflowActivities.Base;
using Microsoft.Xrm.Sdk.Workflow;

namespace WorkflowActivities.Implementations
{
    [DisplayName("Phone Number Resolver")]
    [Description("Resolves a phone number into Country ISO, Calling Code, and formatted Phone Number.")]
    public class PhoneNumberResolver : WorkflowActivityBase
    {
        private struct CountryPhoneFormat
        {
            public string Iso { get; set; }
            public int Code { get; set; }
            public int MinLength { get; set; }
            public int MaxLength { get; set; }
            public string Regex { get; set; }
            public string[] AreaCodes { get; set; }  // Keep this for potential future use with area codes
        }

        private static readonly Dictionary<string, List<CountryPhoneFormat>> CountryCodes = new Dictionary<string, List<CountryPhoneFormat>>
        {
            // EU Countries
            {"43", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AT", Code = 43, MinLength = 9, MaxLength = 13, Regex = @"^[1-9][0-9]{8,12}$", AreaCodes = null } 
            }},
            {"32", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BE", Code = 32, MinLength = 8, MaxLength = 9, Regex = @"^[1-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"359", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BG", Code = 359, MinLength = 8, MaxLength = 9, Regex = @"^[2-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"385", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "HR", Code = 385, MinLength = 8, MaxLength = 9, Regex = @"^[1-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"357", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CY", Code = 357, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"420", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CZ", Code = 420, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"45", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "DK", Code = 45, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"372", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "EE", Code = 372, MinLength = 7, MaxLength = 8, Regex = @"^[3-9][0-9]{6,7}$", AreaCodes = null } 
            }},
            {"358", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "FI", Code = 358, MinLength = 6, MaxLength = 12, Regex = @"^[1-9][0-9]{5,11}$", AreaCodes = null } 
            }},
            {"33", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "FR", Code = 33, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"49", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "DE", Code = 49, MinLength = 10, MaxLength = 11, Regex = @"^[1-9][0-9]{9,10}$", AreaCodes = null } 
            }},
            {"30", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GR", Code = 30, MinLength = 10, MaxLength = 10, Regex = @"^[2-9][0-9]{9}$", AreaCodes = null } 
            }},
            {"36", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "HU", Code = 36, MinLength = 8, MaxLength = 9, Regex = @"^[1-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"353", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IE", Code = 353, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"39", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IT", Code = 39, MinLength = 9, MaxLength = 11, Regex = @"^[3][0-9]{8,10}$", AreaCodes = null } 
            }},
            {"371", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LV", Code = 371, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"370", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LT", Code = 370, MinLength = 8, MaxLength = 8, Regex = @"^[3-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"352", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LU", Code = 352, MinLength = 4, MaxLength = 12, Regex = @"^[2-9][0-9]{3,11}$", AreaCodes = null } 
            }},
            {"356", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MT", Code = 356, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"31", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NL", Code = 31, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"48", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PL", Code = 48, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"351", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PT", Code = 351, MinLength = 9, MaxLength = 9, Regex = @"^[2-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"40", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "RO", Code = 40, MinLength = 9, MaxLength = 9, Regex = @"^[2-8][0-9]{8}$", AreaCodes = null } 
            }},
            {"421", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SK", Code = 421, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"386", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SI", Code = 386, MinLength = 8, MaxLength = 8, Regex = @"^[1-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"34", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "ES", Code = 34, MinLength = 9, MaxLength = 9, Regex = @"^[6-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"46", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SE", Code = 46, MinLength = 7, MaxLength = 13, Regex = @"^[1-9][0-9]{6,12}$", AreaCodes = null } 
            }},

            // EUROPA (rest)
            {"355", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AL", Code = 355, MinLength = 9, MaxLength = 9, Regex = @"^[6-7][0-9]{8}$", AreaCodes = null } 
            }},
            {"376", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AD", Code = 376, MinLength = 6, MaxLength = 9, Regex = @"^[3-7][0-9]{5,8}$", AreaCodes = null } 
            }},
            {"374", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AM", Code = 374, MinLength = 8, MaxLength = 8, Regex = @"^[1-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"994", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AZ", Code = 994, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"375", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BY", Code = 375, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"387", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BA", Code = 387, MinLength = 8, MaxLength = 8, Regex = @"^[3-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"995", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GE", Code = 995, MinLength = 9, MaxLength = 9, Regex = @"^[5-7][0-9]{8}$", AreaCodes = null } 
            }},
            {"354", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IS", Code = 354, MinLength = 7, MaxLength = 9, Regex = @"^[4-8][0-9]{6,8}$", AreaCodes = null } 
            }},
            {"383", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "XK", Code = 383, MinLength = 8, MaxLength = 8, Regex = @"^[4-5][0-9]{7}$", AreaCodes = null } 
            }},
            {"423", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LI", Code = 423, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"377", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MC", Code = 377, MinLength = 8, MaxLength = 9, Regex = @"^[4-6][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"382", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "ME", Code = 382, MinLength = 8, MaxLength = 8, Regex = @"^[6-7][0-9]{7}$", AreaCodes = null } 
            }},
            {"389", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MK", Code = 389, MinLength = 8, MaxLength = 8, Regex = @"^[2-3][0-9]{7}$", AreaCodes = null } 
            }},
            {"47", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NO", Code = 47, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"381", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "RS", Code = 381, MinLength = 8, MaxLength = 9, Regex = @"^[6-7][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"378", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SM", Code = 378, MinLength = 6, MaxLength = 10, Regex = @"^[5-7][0-9]{5,9}$", AreaCodes = null } 
            }},
            {"41", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CH", Code = 41, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"90", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TR", Code = 90, MinLength = 10, MaxLength = 10, Regex = @"^[5][0-9]{9}$", AreaCodes = null } 
            }},
            {"380", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "UA", Code = 380, MinLength = 9, MaxLength = 9, Regex = @"^[3-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"379", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VA", Code = 379, MinLength = 10, MaxLength = 10, Regex = @"^[0-9]{10}$", AreaCodes = null } 
            }},
            {"44", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GB", Code = 44, MinLength = 10, MaxLength = 10, Regex = @"^[1-9][0-9]{9}$", AreaCodes = null } 
            }},
            {"298", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "FO", Code = 298, MinLength = 6, MaxLength = 6, Regex = @"^[2-9][0-9]{5}$", AreaCodes = null } 
            }},
            {"350", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GI", Code = 350, MinLength = 8, MaxLength = 8, Regex = @"^[5-6][0-9]{7}$", AreaCodes = null } 
            }},
            {"299", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GL", Code = 299, MinLength = 6, MaxLength = 6, Regex = @"^[2-9][0-9]{5}$", AreaCodes = null } 
            }},

            // NORTH AMERICA
            {"1", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "US", Code = 1, MinLength = 10, MaxLength = 10, Regex = @"^[2-9][0-9]{9}$", AreaCodes = null },
                new CountryPhoneFormat { Iso = "CA", Code = 1, MinLength = 10, MaxLength = 10, Regex = @"^[2-9][0-9]{9}$", AreaCodes = null }
            }},
            {"52", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MX", Code = 52, MinLength = 10, MaxLength = 10, Regex = @"^[1-9][0-9]{9}$", AreaCodes = null } 
            }},
            {"1242", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BS", Code = 1242, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1246", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BB", Code = 1246, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1441", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BM", Code = 1441, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1345", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KY", Code = 1345, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"506", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CR", Code = 506, MinLength = 8, MaxLength = 8, Regex = @"^[2-8][0-9]{7}$", AreaCodes = null } 
            }},
            {"53", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CU", Code = 53, MinLength = 8, MaxLength = 8, Regex = @"^[5][0-9]{7}$", AreaCodes = null } 
            }},
            {"1809", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "DO", Code = 1809, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"503", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SV", Code = 503, MinLength = 8, MaxLength = 8, Regex = @"^[267][0-9]{7}$", AreaCodes = null } 
            }},
            {"1473", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GD", Code = 1473, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"502", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GT", Code = 502, MinLength = 8, MaxLength = 8, Regex = @"^[2-7][0-9]{7}$", AreaCodes = null } 
            }},
            {"509", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "HT", Code = 509, MinLength = 8, MaxLength = 8, Regex = @"^[2-4][0-9]{7}$", AreaCodes = null } 
            }},
            {"504", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "HN", Code = 504, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"1876", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "JM", Code = 1876, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"505", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NI", Code = 505, MinLength = 8, MaxLength = 8, Regex = @"^[2-8][0-9]{7}$", AreaCodes = null } 
            }},
            {"507", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PA", Code = 507, MinLength = 8, MaxLength = 8, Regex = @"^[2-8][0-9]{7}$", AreaCodes = null } 
            }},
            {"1787", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PR", Code = 1787, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1869", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KN", Code = 1869, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1758", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LC", Code = 1758, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1784", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VC", Code = 1784, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1868", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TT", Code = 1868, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1649", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TC", Code = 1649, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},
            {"1340", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VI", Code = 1340, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},

            // ASIA
            {"86", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CN", Code = 86, MinLength = 11, MaxLength = 11, Regex = @"^1[3-9][0-9]{9}$", AreaCodes = null } 
            }},
            {"81", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "JP", Code = 81, MinLength = 10, MaxLength = 10, Regex = @"^[0][789][0-9]{8}$", AreaCodes = null } 
            }},
            {"82", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KR", Code = 82, MinLength = 9, MaxLength = 10, Regex = @"^01[0-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"852", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "HK", Code = 852, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"886", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TW", Code = 886, MinLength = 9, MaxLength = 9, Regex = @"^[2-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"65", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SG", Code = 65, MinLength = 8, MaxLength = 8, Regex = @"^[689][0-9]{7}$", AreaCodes = null } 
            }},
            {"91", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IN", Code = 91, MinLength = 10, MaxLength = 10, Regex = @"^[6789][0-9]{9}$", AreaCodes = null } 
            }},
            {"62", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "ID", Code = 62, MinLength = 10, MaxLength = 12, Regex = @"^8[1-9][0-9]{8,10}$", AreaCodes = null } 
            }},
            {"60", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MY", Code = 60, MinLength = 9, MaxLength = 10, Regex = @"^1[0-9]{8,9}$", AreaCodes = null } 
            }},
            {"66", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TH", Code = 66, MinLength = 9, MaxLength = 9, Regex = @"^[689][0-9]{8}$", AreaCodes = null } 
            }},
            {"84", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VN", Code = 84, MinLength = 9, MaxLength = 10, Regex = @"^[1-9][0-9]{8,9}$", AreaCodes = null } 
            }},
            {"63", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PH", Code = 63, MinLength = 10, MaxLength = 10, Regex = @"^9[0-9]{9}$", AreaCodes = null } 
            }},
            {"855", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KH", Code = 855, MinLength = 8, MaxLength = 9, Regex = @"^[1-9][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"856", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LA", Code = 856, MinLength = 8, MaxLength = 9, Regex = @"^[2-8][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"95", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MM", Code = 95, MinLength = 8, MaxLength = 10, Regex = @"^[9][0-9]{7,9}$", AreaCodes = null } 
            }},
            {"880", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BD", Code = 880, MinLength = 10, MaxLength = 10, Regex = @"^1[0-9]{9}$", AreaCodes = null } 
            }},
            {"977", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NP", Code = 977, MinLength = 10, MaxLength = 10, Regex = @"^[9][8][0-9]{8}$", AreaCodes = null } 
            }},
            {"94", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LK", Code = 94, MinLength = 9, MaxLength = 9, Regex = @"^[1-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"92", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PK", Code = 92, MinLength = 10, MaxLength = 10, Regex = @"^3[0-9]{9}$", AreaCodes = null } 
            }},
            {"93", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AF", Code = 93, MinLength = 9, MaxLength = 9, Regex = @"^[7][0-9]{8}$", AreaCodes = null } 
            }},
            {"973", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BH", Code = 973, MinLength = 8, MaxLength = 8, Regex = @"^[3][0-9]{7}$", AreaCodes = null } 
            }},
            {"98", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IR", Code = 98, MinLength = 10, MaxLength = 10, Regex = @"^9[0-9]{9}$", AreaCodes = null } 
            }},
            {"964", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IQ", Code = 964, MinLength = 10, MaxLength = 10, Regex = @"^7[0-9]{9}$", AreaCodes = null } 
            }},
            {"972", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "IL", Code = 972, MinLength = 9, MaxLength = 9, Regex = @"^5[0-9]{8}$", AreaCodes = null } 
            }},
            {"962", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "JO", Code = 962, MinLength = 9, MaxLength = 9, Regex = @"^7[789][0-9]{7}$", AreaCodes = null } 
            }},
            {"965", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KW", Code = 965, MinLength = 8, MaxLength = 8, Regex = @"^[569][0-9]{7}$", AreaCodes = null } 
            }},
            {"961", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "LB", Code = 961, MinLength = 7, MaxLength = 8, Regex = @"^[3-9][0-9]{6,7}$", AreaCodes = null } 
            }},
            {"968", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "OM", Code = 968, MinLength = 8, MaxLength = 8, Regex = @"^[79][0-9]{7}$", AreaCodes = null } 
            }},
            {"974", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "QA", Code = 974, MinLength = 8, MaxLength = 8, Regex = @"^[3-7][0-9]{7}$", AreaCodes = null } 
            }},
            {"966", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SA", Code = 966, MinLength = 9, MaxLength = 9, Regex = @"^5[0-9]{8}$", AreaCodes = null } 
            }},
            {"971", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AE", Code = 971, MinLength = 9, MaxLength = 9, Regex = @"^5[0-9]{8}$", AreaCodes = null } 
            }},
            {"967", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "YE", Code = 967, MinLength = 9, MaxLength = 9, Regex = @"^7[0-9]{8}$", AreaCodes = null } 
            }},

            // South America
            {"54", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AR", Code = 54, MinLength = 10, MaxLength = 10, Regex = @"^9[0-9]{9}$", AreaCodes = null } 
            }},
            {"591", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BO", Code = 591, MinLength = 8, MaxLength = 8, Regex = @"^[67][0-9]{7}$", AreaCodes = null } 
            }},
            {"55", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "BR", Code = 55, MinLength = 10, MaxLength = 11, Regex = @"^[1-9][1-9][0-9]{8,9}$", AreaCodes = null } 
            }},
            {"56", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CL", Code = 56, MinLength = 9, MaxLength = 9, Regex = @"^9[0-9]{8}$", AreaCodes = null } 
            }},
            {"57", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CO", Code = 57, MinLength = 10, MaxLength = 10, Regex = @"^3[0-9]{9}$", AreaCodes = null } 
            }},
            {"593", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "EC", Code = 593, MinLength = 9, MaxLength = 9, Regex = @"^[89][0-9]{8}$", AreaCodes = null } 
            }},
            {"595", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PY", Code = 595, MinLength = 9, MaxLength = 9, Regex = @"^9[0-9]{8}$", AreaCodes = null } 
            }},
            {"51", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PE", Code = 51, MinLength = 9, MaxLength = 9, Regex = @"^9[0-9]{8}$", AreaCodes = null } 
            }},
            {"598", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "UY", Code = 598, MinLength = 8, MaxLength = 8, Regex = @"^9[0-9]{7}$", AreaCodes = null } 
            }},
            {"58", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VE", Code = 58, MinLength = 10, MaxLength = 10, Regex = @"^4[0-9]{9}$", AreaCodes = null } 
            }},

            // Africa
            {"213", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "DZ", Code = 213, MinLength = 9, MaxLength = 9, Regex = @"^[567][0-9]{8}$", AreaCodes = null } 
            }},
            {"20", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "EG", Code = 20, MinLength = 10, MaxLength = 10, Regex = @"^1[0-9]{9}$", AreaCodes = null } 
            }},
            {"233", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GH", Code = 233, MinLength = 9, MaxLength = 9, Regex = @"^[2-9][0-9]{8}$", AreaCodes = null } 
            }},
            {"254", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "KE", Code = 254, MinLength = 9, MaxLength = 9, Regex = @"^[17][0-9]{8}$", AreaCodes = null } 
            }},
            {"212", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "MA", Code = 212, MinLength = 9, MaxLength = 9, Regex = @"^[67][0-9]{8}$", AreaCodes = null } 
            }},
            {"234", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NG", Code = 234, MinLength = 10, MaxLength = 10, Regex = @"^[789][0-9]{9}$", AreaCodes = null } 
            }},
            {"27", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "ZA", Code = 27, MinLength = 9, MaxLength = 9, Regex = @"^[6-8][0-9]{8}$", AreaCodes = null } 
            }},
            {"216", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TN", Code = 216, MinLength = 8, MaxLength = 8, Regex = @"^[2-9][0-9]{7}$", AreaCodes = null } 
            }},
            {"256", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "UG", Code = 256, MinLength = 9, MaxLength = 9, Regex = @"^[7][0-9]{8}$", AreaCodes = null } 
            }},
            {"255", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TZ", Code = 255, MinLength = 9, MaxLength = 9, Regex = @"^[67][0-9]{8}$", AreaCodes = null } 
            }},
            {"251", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "ET", Code = 251, MinLength = 9, MaxLength = 9, Regex = @"^9[0-9]{8}$", AreaCodes = null } 
            }},
            {"250", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "RW", Code = 250, MinLength = 9, MaxLength = 9, Regex = @"^7[0-9]{8}$", AreaCodes = null } 
            }},
            {"237", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CM", Code = 237, MinLength = 9, MaxLength = 9, Regex = @"^[67][0-9]{8}$", AreaCodes = null } 
            }},
            {"225", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "CI", Code = 225, MinLength = 10, MaxLength = 10, Regex = @"^[01][0-9]{9}$", AreaCodes = null } 
            }},
            {"220", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "GM", Code = 220, MinLength = 7, MaxLength = 7, Regex = @"^[2-9][0-9]{6}$", AreaCodes = null } 
            }},

            // Oceania
            {"61", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "AU", Code = 61, MinLength = 9, MaxLength = 9, Regex = @"^4[0-9]{8}$", AreaCodes = null } 
            }},
            {"64", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "NZ", Code = 64, MinLength = 8, MaxLength = 9, Regex = @"^[278][0-9]{7,8}$", AreaCodes = null } 
            }},
            {"679", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "FJ", Code = 679, MinLength = 7, MaxLength = 7, Regex = @"^[79][0-9]{6}$", AreaCodes = null } 
            }},
            {"675", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "PG", Code = 675, MinLength = 8, MaxLength = 8, Regex = @"^[7][0-9]{7}$", AreaCodes = null } 
            }},
            {"685", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "WS", Code = 685, MinLength = 5, MaxLength = 7, Regex = @"^[68][0-9]{4,6}$", AreaCodes = null } 
            }},
            {"677", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "SB", Code = 677, MinLength = 7, MaxLength = 7, Regex = @"^[7-8][0-9]{6}$", AreaCodes = null } 
            }},
            {"676", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "TO", Code = 676, MinLength = 5, MaxLength = 7, Regex = @"^[78][0-9]{4,6}$", AreaCodes = null } 
            }},
            {"678", new List<CountryPhoneFormat> { 
                new CountryPhoneFormat { Iso = "VU", Code = 678, MinLength = 5, MaxLength = 7, Regex = @"^[57][0-9]{4,6}$", AreaCodes = null } 
            }}
        };
        
        [Input("Phone Number")]
        [RequiredArgument]
        public InArgument<string> PhoneNumberInput { get; set; }

        [Output("Country ISO")]
        public OutArgument<string> CountryISO { get; set; }

        [Output("Country Calling Code")]
        public OutArgument<int> CountryCallingCode { get; set; }

        [Output("Phone Number")]
        public OutArgument<string> FormattedPhoneNumber { get; set; }

        private bool IsValidPhoneNumber(string nationalNumber, CountryPhoneFormat format)
        {
            if (nationalNumber.Length < format.MinLength || nationalNumber.Length > format.MaxLength)
                return false;

            return string.IsNullOrEmpty(format.Regex) || Regex.IsMatch(nationalNumber, format.Regex);
        }

        protected override void ExecuteWorkflowLogic(CodeActivityContext executionContext)
        {
            string phoneNumber = PhoneNumberInput.Get(executionContext);

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                SetReturnValues(executionContext, false, "Phone number input cannot be null or empty.");
                return;
            }

            try
            {
                phoneNumber = Regex.Replace(phoneNumber, @"[^\d+]", "");

                if (phoneNumber.StartsWith("+"))
                {
                    phoneNumber = phoneNumber.Substring(1);
                }
                else if (phoneNumber.StartsWith("00"))
                {
                    phoneNumber = phoneNumber.Substring(2);
                }

                string matchedCountryCode = null;
                List<CountryPhoneFormat> matchedFormats = null;
                foreach (var code in CountryCodes.Keys)
                {
                    if (phoneNumber.StartsWith(code))
                    {
                        if (matchedCountryCode == null || code.Length > matchedCountryCode.Length)
                        {
                            matchedCountryCode = code;
                            matchedFormats = CountryCodes[code];
                        }
                    }
                }

                if (matchedCountryCode == null || matchedFormats == null)
                {
                    SetReturnValues(executionContext, false, "Could not determine country code.");
                    return;
                }

                string nationalNumber = phoneNumber.Substring(matchedCountryCode.Length);
                CountryPhoneFormat? matchedFormat = null;

                foreach (var format in matchedFormats)
                {
                    if (nationalNumber.Length >= format.MinLength && 
                        nationalNumber.Length <= format.MaxLength && 
                        Regex.IsMatch(nationalNumber, format.Regex))
                    {
                        matchedFormat = format;
                        break;
                    }
                }

                if (!matchedFormat.HasValue)
                {
                    SetReturnValues(executionContext, false, $"Invalid phone number format for given country code.");
                    return;
                }

                string formattedNumberWithoutWhitespace = Regex.Replace(nationalNumber, @"\s+", "");
                CountryISO.Set(executionContext, matchedFormat.Value.Iso);
                CountryCallingCode.Set(executionContext, matchedFormat.Value.Code);
                FormattedPhoneNumber.Set(executionContext, formattedNumberWithoutWhitespace);

                SetReturnValues(executionContext, true, "Phone number resolved successfully.");
            }
            catch (Exception ex)
            {
                SetReturnValues(executionContext, false, $"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}