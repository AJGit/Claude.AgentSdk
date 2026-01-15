using System.Text.Json;
using ClosedXML.Excel;
using Claude.AgentSdk.Tools;

namespace Claude.AgentSdk.ExcelAgent;

/// <summary>
/// Custom MCP tools for Excel file operations using ClosedXML.
/// </summary>
public class ExcelTools
{
    private readonly string _outputDir;

    public ExcelTools(string outputDir)
    {
        _outputDir = outputDir;
        Directory.CreateDirectory(outputDir);
    }

    /// <summary>
    /// Registers all Excel tools with the MCP server.
    /// </summary>
    public void RegisterTools(McpToolServer server)
    {
        server.RegisterTool<CreateWorkbookInput>(
            "create_workbook",
            "Create a new Excel workbook with specified sheets. Returns the file path.",
            CreateWorkbookAsync);

        server.RegisterTool<AddDataInput>(
            "add_data",
            "Add data to a specific range in an Excel workbook. Supports headers, rows, and formulas.",
            AddDataAsync);

        server.RegisterTool<FormatRangeInput>(
            "format_range",
            "Apply formatting to a range of cells (bold, colors, alignment, number format).",
            FormatRangeAsync);

        server.RegisterTool<AddFormulaInput>(
            "add_formula",
            "Add a formula to a specific cell.",
            AddFormulaAsync);

        server.RegisterTool<CreateChartInput>(
            "create_chart",
            "Create a chart from data in the workbook.",
            CreateChartAsync);

        server.RegisterTool<ListWorkbooksInput>(
            "list_workbooks",
            "List all Excel files in the output directory.",
            ListWorkbooksAsync);

        server.RegisterTool<ReadWorkbookInput>(
            "read_workbook",
            "Read the contents of an Excel workbook for inspection.",
            ReadWorkbookAsync);
    }

    private Task<ToolResult> CreateWorkbookAsync(CreateWorkbookInput input, CancellationToken ct)
    {
        try
        {
            var filePath = Path.Combine(_outputDir, input.FileName);
            if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                filePath += ".xlsx";

            using var workbook = new XLWorkbook();

            if (input.Sheets?.Length > 0)
            {
                foreach (var sheetName in input.Sheets)
                {
                    workbook.Worksheets.Add(sheetName);
                }
            }
            else
            {
                workbook.Worksheets.Add("Sheet1");
            }

            workbook.SaveAs(filePath);

            return Task.FromResult(ToolResult.Text($"Created workbook: {filePath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to create workbook: {ex.Message}"));
        }
    }

    private Task<ToolResult> AddDataAsync(AddDataInput input, CancellationToken ct)
    {
        try
        {
            var filePath = ResolveFilePath(input.FileName);
            if (!File.Exists(filePath))
                return Task.FromResult(ToolResult.Error($"Workbook not found: {filePath}"));

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, input.SheetName);

            var startRow = input.StartRow ?? 1;
            var startCol = input.StartColumn ?? 1;

            // Add headers if provided
            if (input.Headers?.Length > 0)
            {
                for (int i = 0; i < input.Headers.Length; i++)
                {
                    var cell = worksheet.Cell(startRow, startCol + i);
                    cell.Value = input.Headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                startRow++;
            }

            // Add data rows
            if (input.Rows?.Length > 0)
            {
                for (int rowIdx = 0; rowIdx < input.Rows.Length; rowIdx++)
                {
                    var row = input.Rows[rowIdx];
                    for (int colIdx = 0; colIdx < row.Length; colIdx++)
                    {
                        var cell = worksheet.Cell(startRow + rowIdx, startCol + colIdx);
                        var value = row[colIdx];

                        if (value is JsonElement jsonElement)
                        {
                            SetCellValue(cell, jsonElement);
                        }
                        else if (value != null)
                        {
                            cell.Value = value.ToString();
                        }
                    }
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            workbook.Save();

            var rowCount = input.Rows?.Length ?? 0;
            return Task.FromResult(ToolResult.Text($"Added {rowCount} rows to {input.SheetName ?? "Sheet1"}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to add data: {ex.Message}"));
        }
    }

    private Task<ToolResult> FormatRangeAsync(FormatRangeInput input, CancellationToken ct)
    {
        try
        {
            var filePath = ResolveFilePath(input.FileName);
            if (!File.Exists(filePath))
                return Task.FromResult(ToolResult.Error($"Workbook not found: {filePath}"));

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, input.SheetName);
            var range = worksheet.Range(input.Range);

            if (input.Bold.HasValue)
                range.Style.Font.Bold = input.Bold.Value;

            if (input.Italic.HasValue)
                range.Style.Font.Italic = input.Italic.Value;

            if (!string.IsNullOrEmpty(input.FontColor))
                range.Style.Font.FontColor = XLColor.FromHtml(input.FontColor);

            if (!string.IsNullOrEmpty(input.BackgroundColor))
                range.Style.Fill.BackgroundColor = XLColor.FromHtml(input.BackgroundColor);

            if (!string.IsNullOrEmpty(input.NumberFormat))
                range.Style.NumberFormat.Format = input.NumberFormat;

            if (!string.IsNullOrEmpty(input.HorizontalAlignment))
            {
                range.Style.Alignment.Horizontal = input.HorizontalAlignment.ToLower() switch
                {
                    "left" => XLAlignmentHorizontalValues.Left,
                    "center" => XLAlignmentHorizontalValues.Center,
                    "right" => XLAlignmentHorizontalValues.Right,
                    _ => XLAlignmentHorizontalValues.General
                };
            }

            if (input.AddBorder.HasValue && input.AddBorder.Value)
            {
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            workbook.Save();

            return Task.FromResult(ToolResult.Text($"Formatted range {input.Range}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to format range: {ex.Message}"));
        }
    }

    private Task<ToolResult> AddFormulaAsync(AddFormulaInput input, CancellationToken ct)
    {
        try
        {
            var filePath = ResolveFilePath(input.FileName);
            if (!File.Exists(filePath))
                return Task.FromResult(ToolResult.Error($"Workbook not found: {filePath}"));

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, input.SheetName);
            var cell = worksheet.Cell(input.Cell);

            cell.FormulaA1 = input.Formula;

            if (!string.IsNullOrEmpty(input.NumberFormat))
                cell.Style.NumberFormat.Format = input.NumberFormat;

            workbook.Save();

            return Task.FromResult(ToolResult.Text($"Added formula to {input.Cell}: {input.Formula}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to add formula: {ex.Message}"));
        }
    }

    private Task<ToolResult> CreateChartAsync(CreateChartInput input, CancellationToken ct)
    {
        try
        {
            // Note: ClosedXML has limited chart support. For full chart support,
            // consider using EPPlus or copying an existing chart template.
            return Task.FromResult(ToolResult.Text(
                "Chart creation is not fully supported in ClosedXML. " +
                "Consider creating the data table and opening in Excel to add charts, " +
                "or use a template workbook with pre-defined charts."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to create chart: {ex.Message}"));
        }
    }

    private Task<ToolResult> ListWorkbooksAsync(ListWorkbooksInput input, CancellationToken ct)
    {
        try
        {
            var files = Directory.GetFiles(_outputDir, "*.xlsx")
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    modified = File.GetLastWriteTime(f)
                })
                .ToArray();

            if (files.Length == 0)
                return Task.FromResult(ToolResult.Text("No Excel files found in output directory."));

            var result = string.Join("\n", files.Select(f =>
                $"- {f.name} ({f.size:N0} bytes, modified {f.modified:g})"));

            return Task.FromResult(ToolResult.Text($"Excel files:\n{result}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to list workbooks: {ex.Message}"));
        }
    }

    private Task<ToolResult> ReadWorkbookAsync(ReadWorkbookInput input, CancellationToken ct)
    {
        try
        {
            var filePath = ResolveFilePath(input.FileName);
            if (!File.Exists(filePath))
                return Task.FromResult(ToolResult.Error($"Workbook not found: {filePath}"));

            using var workbook = new XLWorkbook(filePath);
            var results = new List<string>();

            foreach (var worksheet in workbook.Worksheets)
            {
                results.Add($"\n## Sheet: {worksheet.Name}");

                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    results.Add("(empty sheet)");
                    continue;
                }

                var maxRows = Math.Min(input.MaxRows ?? 20, usedRange.RowCount());
                var maxCols = Math.Min(input.MaxColumns ?? 10, usedRange.ColumnCount());

                // Build a simple table representation
                for (int row = 1; row <= maxRows; row++)
                {
                    var cells = new List<string>();
                    for (int col = 1; col <= maxCols; col++)
                    {
                        var cell = usedRange.Cell(row, col);
                        var value = cell.HasFormula
                            ? $"={cell.FormulaA1}"
                            : cell.GetString();
                        cells.Add(value.Length > 20 ? value[..17] + "..." : value);
                    }
                    results.Add($"| {string.Join(" | ", cells)} |");
                }

                if (usedRange.RowCount() > maxRows || usedRange.ColumnCount() > maxCols)
                {
                    results.Add($"... (showing {maxRows}x{maxCols} of {usedRange.RowCount()}x{usedRange.ColumnCount()})");
                }
            }

            return Task.FromResult(ToolResult.Text(string.Join("\n", results)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Error($"Failed to read workbook: {ex.Message}"));
        }
    }

    private string ResolveFilePath(string fileName)
    {
        if (Path.IsPathRooted(fileName))
            return fileName;

        var path = Path.Combine(_outputDir, fileName);
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            path += ".xlsx";

        return path;
    }

    private static void SetCellValue(IXLCell cell, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                cell.Value = element.GetDouble();
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                cell.Value = element.GetBoolean();
                break;
            case JsonValueKind.String:
                var str = element.GetString() ?? "";
                if (str.StartsWith('='))
                    cell.FormulaA1 = str[1..];
                else
                    cell.Value = str;
                break;
            case JsonValueKind.Object:
                // Handle objects with a "formula" property (e.g., {"formula":"=SUM(A1:A10)"})
                if (element.TryGetProperty("formula", out var formulaElement))
                {
                    var formula = formulaElement.GetString() ?? "";
                    // FormulaA1 expects formula without leading '='
                    cell.FormulaA1 = formula.StartsWith('=') ? formula[1..] : formula;
                }
                // Handle objects with a "value" property (common from Claude)
                else if (element.TryGetProperty("value", out var valueElement))
                {
                    SetCellValue(cell, valueElement);
                }
                else
                {
                    cell.Value = element.ToString();
                }
                break;
            default:
                cell.Value = element.ToString();
                break;
        }
    }

    private static IXLWorksheet GetWorksheet(XLWorkbook workbook, string? sheetName)
    {
        if (!string.IsNullOrEmpty(sheetName))
            return workbook.Worksheet(sheetName);

        // Default to first worksheet
        return workbook.Worksheet(1);
    }
}

// Input types for Excel tools

public record CreateWorkbookInput
{
    public required string FileName { get; init; }
    public string[]? Sheets { get; init; }
}

public record AddDataInput
{
    public required string FileName { get; init; }
    public string? SheetName { get; init; }
    public int? StartRow { get; init; }
    public int? StartColumn { get; init; }
    public string[]? Headers { get; init; }
    public object[][]? Rows { get; init; }
}

public record FormatRangeInput
{
    public required string FileName { get; init; }
    public string? SheetName { get; init; }
    public required string Range { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public string? FontColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? NumberFormat { get; init; }
    public string? HorizontalAlignment { get; init; }
    public bool? AddBorder { get; init; }
}

public record AddFormulaInput
{
    public required string FileName { get; init; }
    public string? SheetName { get; init; }
    public required string Cell { get; init; }
    public required string Formula { get; init; }
    public string? NumberFormat { get; init; }
}

public record CreateChartInput
{
    public required string FileName { get; init; }
    public string? SheetName { get; init; }
    public required string DataRange { get; init; }
    public required string ChartType { get; init; }
    public string? Title { get; init; }
}

public record ListWorkbooksInput;

public record ReadWorkbookInput
{
    public required string FileName { get; init; }
    public int? MaxRows { get; init; }
    public int? MaxColumns { get; init; }
}
