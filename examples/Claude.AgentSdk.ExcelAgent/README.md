# Excel Agent Example

Create professional Excel spreadsheets using AI with custom MCP tools powered by ClosedXML.

## What This Example Demonstrates

- **Compile-time tool registration** using `[GenerateToolRegistration]` attribute
- **Compile-time schema generation** using `[GenerateSchema]` attribute
- **Enhanced `[ClaudeTool]`** with Categories and TimeoutSeconds properties
- **`[ToolParameter]`** attributes for rich parameter documentation
- **In-process tool server** using `McpToolServer`
- **ClosedXML integration** for Excel file generation

## C#-Centric Features Used

### Fluent Options Builder with Generated Tool Names

```csharp
using Claude.AgentSdk.Builders;
using Claude.AgentSdk.Types;

var excelTools = new ExcelTools(outputDir);
var toolServer = new McpToolServer("excel-tools");
toolServer.RegisterToolsCompiled(excelTools);

var options = new ClaudeAgentOptionsBuilder()
    .WithModel(ModelIdentifier.Sonnet)
    .WithSystemPrompt("You are an Excel spreadsheet assistant...")
    .WithPermissionMode(PermissionMode.AcceptEdits)
    .AddMcpServer("excel-tools", new McpSdkServerConfig
    {
        Name = "excel-tools",
        Instance = toolServer
    })
    // Use generated method for AllowedTools - no manual string lists!
    .AllowTools(excelTools.GetAllowedToolsCompiled("excel-tools"))
    // Also allow standard tools
    .AllowTools(ToolName.Read, ToolName.Write, ToolName.Bash)
    .Build();
```

### Strongly-Typed MCP Tool Names

```csharp
using Claude.AgentSdk.Types;

var server = McpServerName.Sdk("excel-tools");
var options = new ClaudeAgentOptions
{
    AllowedTools = [
        server.Tool("create_workbook"),
        server.Tool("add_data"),
        server.Tool("format_range"),
        ToolName.Read,
        ToolName.Write
    ]
};
```

### ModelIdentifier and MCP Server Builder

```csharp
using Claude.AgentSdk.Types;
using Claude.AgentSdk.Builders;

// Type-safe model selection
var options = new ClaudeAgentOptions
{
    ModelId = ModelIdentifier.Sonnet,

    // Fluent MCP server configuration
    McpServers = new McpServerBuilder()
        .AddSdk("excel-tools", toolServer)
        .Build()
};
```

### Parameter Validation

Tool parameters with constraints are validated automatically:

```csharp
[ClaudeTool("format_range", "Format cells")]
public string FormatRange(
    [ToolParameter(Description = "File name")] string fileName,
    [ToolParameter(Description = "Range", Pattern = @"^[A-Z]+\d+:[A-Z]+\d+$")] string range,
    [ToolParameter(Description = "Font size", MinValue = 8, MaxValue = 72)] int? fontSize = null)
{
    // Pattern and MinValue/MaxValue are validated before method runs
    // Invalid inputs like "invalid_range" return ToolResult.Error() automatically
}
```

### Compile-Time Tool Registration with Enhanced Properties

```csharp
[GenerateToolRegistration]  // Enables RegisterToolsCompiled() extension
public class ExcelTools
{
    [ClaudeTool("create_workbook",
        "Create a new Excel workbook with specified sheets. Returns the file path.",
        Categories = ["excel", "file"],    // Multiple categories
        TimeoutSeconds = 10)]              // Timeout for file operations
    public string CreateWorkbook(
        [ToolParameter(Description = "Name of the Excel file to create")] string fileName,
        [ToolParameter(Description = "Optional array of sheet names to create")] string[]? sheets = null)
    {
        // Implementation
    }
}

// No reflection - uses generated code
var excelTools = new ExcelTools(outputDir);
toolServer.RegisterToolsCompiled(excelTools);
```

### Input Types with Schema Generation

```csharp
[GenerateSchema]  // Generates JSON schema at compile-time
public record CreateWorkbookInput
{
    [ToolParameter(Description = "Name of the Excel file to create")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Optional array of sheet names to create")]
    public string[]? Sheets { get; init; }
}

[GenerateSchema]
public record FormatRangeInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Range to format (e.g., 'A1:D10')")]
    public required string Range { get; init; }

    [ToolParameter(Description = "Apply bold formatting")]
    public bool? Bold { get; init; }

    [ToolParameter(Description = "Horizontal alignment ('left', 'center', 'right')")]
    public string? HorizontalAlignment { get; init; }
}
```

## Features

The Excel Agent can create workbooks with:
- Multiple sheets with meaningful names
- Data tables with headers and rows
- Excel formulas (SUM, AVERAGE, IF, VLOOKUP, etc.)
- Professional formatting (colors, fonts, alignment, borders)
- Number formatting (currency, percentages, dates)

## Running the Example

```bash
cd examples/Claude.AgentSdk.ExcelAgent
dotnet run
```

## Example Prompts

```
You: Create a monthly budget tracker with income and expenses

You: Build a sales report with quarterly summaries and year-over-year comparison

You: Make a workout log that tracks exercises, sets, reps, and calculates weekly totals

You: Create an invoice template with line items, subtotal, tax, and total formulas
```

## Available MCP Tools

| Tool              | Description                                                      |
| ----------------- | ---------------------------------------------------------------- |
| `create_workbook` | Create a new Excel file with specified sheets                    |
| `add_data`        | Add headers and data rows to a sheet                             |
| `add_formula`     | Add Excel formulas to cells                                      |
| `format_range`    | Apply formatting (bold, colors, alignment, borders)              |
| `create_chart`    | Create charts (limited support - see note below)                 |
| `list_workbooks`  | List all Excel files in output directory                         |
| `read_workbook`   | Read and display workbook contents                               |

> **Note:** The `create_chart` tool has limited functionality in ClosedXML. For complex charts, consider creating the data table and opening in Excel to add charts, or use a template workbook with pre-defined charts.

## Key Code Patterns

### Defining Tools with Categories and Timeout

```csharp
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Tools;

[GenerateToolRegistration]
public class ExcelTools
{
    [ClaudeTool("add_data",
        "Add data to a specific range in an Excel workbook. Supports headers, rows, and formulas.",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string AddData(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Name of the sheet (optional, defaults to first sheet)")] string? sheetName = null,
        [ToolParameter(Description = "Starting row number (1-based)")] int? startRow = null,
        [ToolParameter(Description = "Starting column number (1-based)")] int? startColumn = null,
        [ToolParameter(Description = "Column headers")] string[]? headers = null,
        [ToolParameter(Description = "Data rows as 2D array")] object[][]? rows = null)
    {
        // Implementation with ClosedXML
    }

    [ClaudeTool("format_range",
        "Apply formatting to a range of cells (bold, colors, alignment, number format).",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string FormatRange(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Range to format (e.g., 'A1:D10')")] string range,
        [ToolParameter(Description = "Name of the sheet")] string? sheetName = null,
        [ToolParameter(Description = "Apply bold formatting")] bool? bold = null,
        [ToolParameter(Description = "Font color as HTML color code (e.g., '#FF0000')")] string? fontColor = null,
        [ToolParameter(Description = "Background color as HTML color code")] string? backgroundColor = null,
        [ToolParameter(Description = "Number format string (e.g., '#,##0.00')")] string? numberFormat = null,
        [ToolParameter(Description = "Horizontal alignment ('left', 'center', 'right')")] string? horizontalAlignment = null,
        [ToolParameter(Description = "Add borders to the range")] bool? addBorder = null)
    {
        // Implementation
    }
}
```

### Configuring the MCP Server

```csharp
var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["excel-tools"] = new McpSdkServerConfig
        {
            Name = "excel-tools",
            Instance = toolServer
        }
    },
    AllowedTools =
    [
        // MCP tools (format: mcp__<server>__<tool>)
        "mcp__excel-tools__create_workbook",
        "mcp__excel-tools__add_data",
        "mcp__excel-tools__format_range",
        "mcp__excel-tools__add_formula",
        "mcp__excel-tools__create_chart",
        "mcp__excel-tools__list_workbooks",
        "mcp__excel-tools__read_workbook",
        // Standard tools the agent may also need
        "Read", "Write", "Bash"
    ]
};
```

## Project Structure

```
Claude.AgentSdk.ExcelAgent/
├── Program.cs              # Main entry point and chat loop
├── ExcelTools.cs           # Custom MCP tool implementations with attributes
├── Claude.AgentSdk.ExcelAgent.csproj
└── output/                 # Generated Excel files (created at runtime)
```

## Source Generator Reference

To use compile-time tool registration, the project references the source generator:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Claude.AgentSdk\Claude.AgentSdk.csproj" />
  <ProjectReference Include="..\..\src\Claude.AgentSdk.Generators\Claude.AgentSdk.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Dependencies

- **ClosedXML** (v0.104.2) - MIT licensed Excel library for .NET

## Example Output

After asking to create a budget tracker:

```
Excel Agent: I'll create a monthly budget tracker for you.

[create_workbook: budget_tracker.xlsx]
[add_data: Budget]
[add_formula: B15]
[format_range: A1:D1]

I've created a budget tracker with:
- Income section with categories (Salary, Freelance, Other)
- Expense section with categories (Rent, Utilities, Food, etc.)
- Summary section with formulas for totals and remaining balance
- Professional formatting with headers and currency formatting

The file has been saved to: output/budget_tracker.xlsx

[3.2s | $0.0089]
```

## Ported From

This example is ported from the official TypeScript/Electron demo:
`claude-agent-sdk-demos/excel-demo/`

Note: The original uses Electron for a desktop UI. This C# port provides a console-based interface while demonstrating the same MCP tool patterns, enhanced with C#-specific source generator features for compile-time tool registration.
