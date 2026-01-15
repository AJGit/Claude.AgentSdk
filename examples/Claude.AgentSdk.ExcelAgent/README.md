# Excel Agent Example

Create professional Excel spreadsheets using AI with custom MCP tools powered by ClosedXML.

## What This Example Demonstrates

- **Custom MCP tools** for Excel file operations
- **In-process tool server** using `McpToolServer`
- **ClosedXML integration** for Excel file generation
- **Professional spreadsheet creation** with formulas, formatting, and styling

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

### Registering Custom MCP Tools

```csharp
var toolServer = new McpToolServer("excel-tools", "1.0.0");

// Register tools using a delegated pattern for better organization
var excelTools = new ExcelTools(outputDir);
excelTools.RegisterTools(toolServer);

// The ExcelTools class registers each tool with multi-line descriptions:
toolServer.RegisterTool<CreateWorkbookInput>(
    "create_workbook",
    """
    Create a new Excel workbook with specified sheets.
    Returns the full path to the created file.
    """,
    CreateWorkbookAsync);
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

### Creating Excel Files with ClosedXML

```csharp
private Task<ToolResult> CreateWorkbookAsync(CreateWorkbookInput input, CancellationToken ct)
{
    using var workbook = new XLWorkbook();

    foreach (var sheetName in input.Sheets)
    {
        workbook.Worksheets.Add(sheetName);
    }

    workbook.SaveAs(filePath);
    return Task.FromResult(ToolResult.Text($"Created workbook: {filePath}"));
}
```

## Project Structure

```
Claude.AgentSdk.ExcelAgent/
├── Program.cs              # Main entry point and chat loop
├── ExcelTools.cs           # Custom MCP tool implementations
├── Claude.AgentSdk.ExcelAgent.csproj
└── output/                 # Generated Excel files (created at runtime)
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

Note: The original uses Electron for a desktop UI. This C# port provides a console-based interface while demonstrating the same MCP tool patterns.
