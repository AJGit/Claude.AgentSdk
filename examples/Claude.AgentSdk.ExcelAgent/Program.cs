/// <summary>
/// Excel Agent - Claude Agent SDK Example
///
/// This example demonstrates:
/// - Custom MCP tools for Excel file generation using ClosedXML
/// - Tools: create_workbook, add_data, format_range, add_formula, list_workbooks, read_workbook
/// - Interactive chat for creating spreadsheets with Claude
///
/// Ported from: claude-agent-sdk-demos/excel-demo/
/// </summary>

using System.Text.Json;
using Claude.AgentSdk;
using Claude.AgentSdk.Messages;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.ExcelAgent;

public static class Program
{
    private const string SystemPrompt = """
        You are an expert Excel spreadsheet agent. You create professional, well-formatted Excel workbooks using the available tools.

        ## Your Capabilities
        You can create Excel workbooks with:
        - Multiple sheets with meaningful names
        - Data tables with headers and rows
        - Formulas for calculations (SUM, AVERAGE, IF, VLOOKUP, etc.)
        - Professional formatting (colors, fonts, alignment, borders)
        - Number formatting (currency, percentages, dates)

        ## Available Tools
        - `create_workbook`: Create a new Excel file with specified sheets
        - `add_data`: Add headers and data rows to a sheet
        - `add_formula`: Add Excel formulas to cells
        - `format_range`: Apply formatting (bold, colors, alignment, borders, number formats)
        - `list_workbooks`: List all Excel files in the output directory
        - `read_workbook`: Read and display workbook contents

        ## Best Practices
        1. **Use formulas, not hardcoded calculations** - Always use Excel formulas (=SUM, =AVERAGE) instead of calculating values in your head
        2. **Professional styling**:
           - Headers: Bold, light gray background
           - Currency: $#,##0.00 format
           - Percentages: 0.0% format
           - Alternating row colors for readability
        3. **Clear structure**: Use separate sheets for different data categories
        4. **Documentation**: Include a summary or instructions sheet when appropriate

        ## Workflow
        1. First, create the workbook with appropriate sheet names
        2. Add data to each sheet
        3. Add formulas for any calculations
        4. Apply formatting to make it professional
        5. Read the workbook to verify the output
        6. Report the file location to the user

        ## Example Formulas
        - Sum: =SUM(B2:B10)
        - Average: =AVERAGE(C2:C20)
        - Percentage: =B2/B$11 (with $ for absolute reference)
        - Conditional: =IF(A2>100,"High","Low")
        - Lookup: =VLOOKUP(A2,Sheet2!A:B,2,FALSE)

        Always create real Excel files that the user can open in Microsoft Excel, Google Sheets, or LibreOffice Calc.
        
        **IMPORTANT** IF YOU CANT USE THE MCP TOOLS SUPPLIED THEN STOP AND INFORM THE USER THAT YOU CANNOT COMPLETE THE REQUEST WITHOUT THEM.
        """;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Excel Agent - Claude Agent SDK");
        Console.WriteLine("==============================================");
        Console.WriteLine();
        Console.WriteLine("Create professional Excel spreadsheets with AI.");
        Console.WriteLine();
        Console.WriteLine("Example prompts:");
        Console.WriteLine("  - Create a monthly budget tracker");
        Console.WriteLine("  - Build a sales report with quarterly summaries");
        Console.WriteLine("  - Make a workout log with weekly totals");
        Console.WriteLine();
        Console.WriteLine("Type 'exit' to quit, 'list' to see generated files.");
        Console.WriteLine();

        // Setup output directory
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();

        // Create MCP tool server with Excel tools
        var toolServer = new McpToolServer("excel-tools", "1.0.0");
        var excelTools = new ExcelTools(outputDir);
        excelTools.RegisterTools(toolServer);

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = SystemPrompt,
            Model = "sonnet",
            MaxTurns = 50,
            PermissionMode = PermissionMode.AcceptEdits,

            // Register the Excel tools MCP server
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["excel-tools"] = new McpSdkServerConfig { Name = "excel-tools", Instance = toolServer }
            },

            // Allow MCP tools plus standard tools
            AllowedTools =
            [
                "mcp__excel-tools__create_workbook",
                "mcp__excel-tools__add_data",
                "mcp__excel-tools__format_range",
                "mcp__excel-tools__add_formula",
                "mcp__excel-tools__create_chart",
                "mcp__excel-tools__list_workbooks",
                "mcp__excel-tools__read_workbook",
                "Read", "Write", "Bash"
            ]
        };

        await RunChatLoopAsync(options, outputDir);
    }

    private static async Task RunChatLoopAsync(ClaudeAgentOptions options, string outputDir)
    {
        while (true)
        {
            try
            {
                await using var client = new ClaudeAgentClient(options);
                await using var session = await client.CreateSessionAsync();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[Connected - ready to create spreadsheets]");
                Console.ResetColor();
                Console.WriteLine();

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("You: ");
                    Console.ResetColor();

                    var input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                        continue;

                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                        input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Goodbye!");
                        return;
                    }

                    if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
                    {
                        ListExcelFiles(outputDir);
                        continue;
                    }

                    if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("[Starting new conversation...]");
                        Console.WriteLine();
                        break;
                    }

                    await session.SendAsync(input);

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Excel Agent: ");
                    Console.ResetColor();

                    var hasOutput = MessageResult.NoMessage;
                    await foreach (var message in session.ReceiveResponseAsync())
                    {
                        hasOutput = ProcessMessage(message);
                        if (hasOutput == MessageResult.Completed)
                            break;
                    }

                    if (hasOutput == MessageResult.Completed)
                        Console.WriteLine();

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError: {ex.Message}");
                Console.ResetColor();

                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                Console.WriteLine();
            }
        }
    }

    private static void ListExcelFiles(string outputDir)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Excel files in output directory:");
        Console.ResetColor();

        var files = Directory.GetFiles(outputDir, "*.xlsx");
        if (files.Length == 0)
        {
            Console.WriteLine("  (no files yet)");
        }
        else
        {
            foreach (var file in files.OrderByDescending(File.GetLastWriteTime))
            {
                var info = new FileInfo(file);
                Console.WriteLine($"  - {info.Name} ({info.Length:N0} bytes, {info.LastWriteTime:g})");
            }
        }
        Console.WriteLine();
    }

    private enum MessageResult
    {
        MoreMessages,
        NoMessage,
        Completed
    }

    private static MessageResult ProcessMessage(Message message)
    {
        switch (message)
        {
            case AssistantMessage assistant:
                foreach (var block in assistant.MessageContent.Content)
                {
                    switch (block)
                    {
                        case TextBlock text:
                            Console.Write(text.Text);
                            return MessageResult.MoreMessages;

                        case ToolUseBlock toolUse:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            var toolName = toolUse.Name.Replace("mcp__excel-tools__", "");
                            Console.Write($"\n[{toolName}");

                            var summary = GetToolInputSummary(toolName, toolUse.Input);
                            if (!string.IsNullOrEmpty(summary))
                                Console.Write($": {summary}");

                            Console.Write("]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;

                        case ThinkingBlock:
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write("[thinking...]");
                            Console.ResetColor();
                            return MessageResult.MoreMessages;
                    }
                }
                break;

            case ResultMessage result:
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"[{result.DurationMs / 1000.0:F1}s | ${result.TotalCostUsd:F4}]");
                Console.ResetColor();
                return MessageResult.Completed;
        }

        return MessageResult.NoMessage;
    }

    private static string? GetToolInputSummary(string toolName, JsonElement? input)
    {
        if (input is null)
            return null;

        try
        {
            var element = input.Value;

            return toolName switch
            {
                "create_workbook" when element.TryGetProperty("file_name", out var fn) =>
                    fn.GetString(),
                "add_data" when element.TryGetProperty("file_name", out var fn) =>
                    fn.GetString(),
                "format_range" when element.TryGetProperty("range", out var r) =>
                    r.GetString(),
                "add_formula" when element.TryGetProperty("cell", out var c) =>
                    c.GetString(),
                "read_workbook" when element.TryGetProperty("file_name", out var fn) =>
                    fn.GetString(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
