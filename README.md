# Morse & Code Workflow Activities

## 📝 Project Overview
This project provides a comprehensive set of custom workflow activities for Microsoft Dynamics CRM/Dataverse, developed by Morse & Code s.r.o. These activities extend the standard workflow capabilities, offering powerful and flexible tools for data manipulation, transformation, and processing.

## 🚀 Key Features

### Workflow Activities Included:
1. **CalculateTwoNumbers**
   - Perform basic arithmetic operations (+, -, *, /) with string inputs
   - Supports decimal calculations
   - Error handling for invalid inputs

2. **CountRecordsActivity**
   - Count records based on FetchXML query
   - Supports pagination
   - Detailed result reporting

3. **DynamicRecordCreationActivity**
   - Create records dynamically using XML mapping
   - Supports field mapping between source and target entities
   - Handles reference field population

4. **ExecuteWorkflowActivity**
   - Execute workflows across multiple records
   - Validates workflow and entity compatibility
   - Comprehensive error tracking

5. **FormatDateFromString**
   - Convert date strings between different formats
   - Uses culture-invariant parsing
   - Flexible input and output format support

6. **GetLargerNumber**
   - Compare numbers (decimal and string-based)
   - Supports both decimal and string inputs
   - Simple, robust comparison logic

7. **GetRandomString**
   - Generate random alphanumeric strings
   - Configurable length
   - Uses cryptographically secure random generation

8. **GetRecordGuid**
   - Retrieve record GUID from multiple sources
   - Supports URL, lookup fields, and FetchXML
   - Flexible GUID extraction methods

9. **ReplaceRegex**
   - Perform regex-based string replacements
   - Supports complex pattern matching
   - Error handling for invalid regex patterns

10. **StringToDecimal**
    - Convert string to decimal
    - Culture-invariant parsing
    - Error handling for invalid inputs

11. **UpdateRecordsActivity**
    - Update multiple records based on FetchXML
    - Supports various attribute types
    - Advanced value conversion and validation

## 🛠 Technical Details

### Development Environment
- **.NET Framework**: 4.7.1
- **Language Version**: C# 9.0
- **Build Type**: Library
- **Signed Assembly**: Yes

### Dependencies
- Microsoft CRM SDK Core Assemblies (8.2.0.0)
- Microsoft CRM SDK Workflow (8.2.0.0)
- Microsoft CRM XrmTooling Core Assembly (8.2.0.1)

## 📦 Installation

### NuGet Package References
```xml
<PackageReference Include="Microsoft.CrmSdk.CoreAssemblies" Version="8.2.0.0" />
<PackageReference Include="Microsoft.CrmSdk.Workflow" Version="8.2.0.0" />
<PackageReference Include="Microsoft.CrmSdk.XrmTooling.CoreAssembly" Version="8.2.0.1" />
```

## 🛠 Build Process

### Building the Project
To build the project in Release configuration, use one of these methods:

#### Using .NET CLI
```bash
dotnet build --configuration Release
```

#### Using MSBuild
```bash
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Current\Bin\MSBuild.exe" MorseCodeActivities.csproj /p:Configuration=Release
```

### Output
- Generates release build of the library
- Creates `MorseCodeActivities.dll` in the `bin/Release` directory
- Resolves and compiles project dependencies

## 🔒 Licensing
- Licensed under the MIT License
- Commercial use allowed
- Proper attribution required
- Copyright © 2025 Morse & Code s.r.o.

## 🚧 Compatibility
- Compatible with Dynamics CRM/Dataverse
- Requires Microsoft CRM SDK
- Tested with version 8.2.0 onwards

## 🤝 Contributing
1. Fork the repository
2. Create feature branches
3. Submit pull requests
4. Follow existing code style and conventions

## 📞 Support
For issues, questions, or support, please contact Morse & Code s.r.o.

## 🌟 Best Practices
- Always validate inputs
- Handle potential exceptions
- Use appropriate error logging
- Leverage built-in error handling mechanisms