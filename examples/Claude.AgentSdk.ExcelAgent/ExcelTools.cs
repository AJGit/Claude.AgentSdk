using System.Text.Json;
using Claude.AgentSdk.Attributes;
using Claude.AgentSdk.Tools;
using ClosedXML.Excel;

namespace Claude.AgentSdk.ExcelAgent;

/// <summary>
/// Custom MCP tools for Excel file operations using ClosedXML.
/// Uses [GenerateToolRegistration] for compile-time tool registration.
/// </summary>
[GenerateToolRegistration]
public class ExcelTools
{
    private readonly string _outputDir;

    public ExcelTools(string outputDir)
    {
        _outputDir = outputDir;
        Directory.CreateDirectory(outputDir);
    }

    /// <summary>
    /// Create a new Excel workbook with specified sheets.
    /// </summary>
    [ClaudeTool("create_workbook",
        "Create a new Excel workbook with specified sheets. Returns the file path.",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string CreateWorkbook(
        [ToolParameter(Description = "Name of the Excel file to create")] string fileName,
        [ToolParameter(Description = "Optional array of sheet names to create")] string[]? sheets = null)
    {
        try
        {
            var filePath = Path.Combine(_outputDir, fileName);
            if (!filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                filePath += ".xlsx";

            using var workbook = new XLWorkbook();

            if (sheets?.Length > 0)
            {
                foreach (var sheetName in sheets)
                {
                    workbook.Worksheets.Add(sheetName);
                }
            }
            else
            {
                workbook.Worksheets.Add("Sheet1");
            }

            workbook.SaveAs(filePath);

            return $"Created workbook: {filePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to create workbook: {ex.Message}";
        }
    }

    /// <summary>
    /// Add data to a specific range in an Excel workbook.
    /// </summary>
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
        try
        {
            var filePath = ResolveFilePath(fileName);
            if (!File.Exists(filePath))
                return $"Workbook not found: {filePath}";

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, sheetName);

            var actualStartRow = startRow ?? 1;
            var actualStartCol = startColumn ?? 1;

            // Add headers if provided
            if (headers?.Length > 0)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(actualStartRow, actualStartCol + i);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                actualStartRow++;
            }

            // Add data rows
            if (rows?.Length > 0)
            {
                for (int rowIdx = 0; rowIdx < rows.Length; rowIdx++)
                {
                    var row = rows[rowIdx];
                    for (int colIdx = 0; colIdx < row.Length; colIdx++)
                    {
                        var cell = worksheet.Cell(actualStartRow + rowIdx, actualStartCol + colIdx);
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

            var rowCount = rows?.Length ?? 0;
            return $"Added {rowCount} rows to {sheetName ?? "Sheet1"}";
        }
        catch (Exception ex)
        {
            return $"Failed to add data: {ex.Message}";
        }
    }

    /// <summary>
    /// Apply formatting to a range of cells.
    /// </summary>
    [ClaudeTool("format_range",
        "Apply formatting to a range of cells (bold, colors, alignment, number format).",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string FormatRange(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Range to format (e.g., 'A1:D10')")] string range,
        [ToolParameter(Description = "Name of the sheet")] string? sheetName = null,
        [ToolParameter(Description = "Apply bold formatting")] bool? bold = null,
        [ToolParameter(Description = "Apply italic formatting")] bool? italic = null,
        [ToolParameter(Description = "Font color as HTML color code (e.g., '#FF0000')")] string? fontColor = null,
        [ToolParameter(Description = "Background color as HTML color code")] string? backgroundColor = null,
        [ToolParameter(Description = "Number format string (e.g., '#,##0.00')")] string? numberFormat = null,
        [ToolParameter(Description = "Horizontal alignment ('left', 'center', 'right')")] string? horizontalAlignment = null,
        [ToolParameter(Description = "Add borders to the range")] bool? addBorder = null)
    {
        try
        {
            var filePath = ResolveFilePath(fileName);
            if (!File.Exists(filePath))
                return $"Workbook not found: {filePath}";

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, sheetName);
            var xlRange = worksheet.Range(range);

            if (bold.HasValue)
                xlRange.Style.Font.Bold = bold.Value;

            if (italic.HasValue)
                xlRange.Style.Font.Italic = italic.Value;

            if (!string.IsNullOrEmpty(fontColor))
                xlRange.Style.Font.FontColor = XLColor.FromHtml(fontColor);

            if (!string.IsNullOrEmpty(backgroundColor))
                xlRange.Style.Fill.BackgroundColor = XLColor.FromHtml(backgroundColor);

            if (!string.IsNullOrEmpty(numberFormat))
                xlRange.Style.NumberFormat.Format = numberFormat;

            if (!string.IsNullOrEmpty(horizontalAlignment))
            {
                xlRange.Style.Alignment.Horizontal = horizontalAlignment.ToLower() switch
                {
                    "left" => XLAlignmentHorizontalValues.Left,
                    "center" => XLAlignmentHorizontalValues.Center,
                    "right" => XLAlignmentHorizontalValues.Right,
                    _ => XLAlignmentHorizontalValues.General
                };
            }

            if (addBorder.HasValue && addBorder.Value)
            {
                xlRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                xlRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            }

            workbook.Save();

            return $"Formatted range {range}";
        }
        catch (Exception ex)
        {
            return $"Failed to format range: {ex.Message}";
        }
    }

    /// <summary>
    /// Add a formula to a specific cell.
    /// </summary>
    [ClaudeTool("add_formula",
        "Add a formula to a specific cell.",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string AddFormula(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Cell address (e.g., 'A1')")] string cell,
        [ToolParameter(Description = "Excel formula (e.g., '=SUM(A1:A10)')")] string formula,
        [ToolParameter(Description = "Name of the sheet")] string? sheetName = null,
        [ToolParameter(Description = "Number format for the result")] string? numberFormat = null)
    {
        try
        {
            var filePath = ResolveFilePath(fileName);
            if (!File.Exists(filePath))
                return $"Workbook not found: {filePath}";

            using var workbook = new XLWorkbook(filePath);
            var worksheet = GetWorksheet(workbook, sheetName);
            var xlCell = worksheet.Cell(cell);

            xlCell.FormulaA1 = formula;

            if (!string.IsNullOrEmpty(numberFormat))
                xlCell.Style.NumberFormat.Format = numberFormat;

            workbook.Save();

            return $"Added formula to {cell}: {formula}";
        }
        catch (Exception ex)
        {
            return $"Failed to add formula: {ex.Message}";
        }
    }

    /// <summary>
    /// Create a chart from data in the workbook.
    /// </summary>
    [ClaudeTool("create_chart",
        "Create a chart from data in the workbook.",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string CreateChart(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Data range for the chart")] string dataRange,
        [ToolParameter(Description = "Chart type (e.g., 'Bar', 'Line', 'Pie')")] string chartType,
        [ToolParameter(Description = "Name of the sheet")] string? sheetName = null,
        [ToolParameter(Description = "Chart title")] string? title = null)
    {
        // Note: ClosedXML has limited chart support. For full chart support,
        // consider using EPPlus or copying an existing chart template.
        return "Chart creation is not fully supported in ClosedXML. " +
               "Consider creating the data table and opening in Excel to add charts, " +
               "or use a template workbook with pre-defined charts.";
    }

    /// <summary>
    /// List all Excel files in the output directory.
    /// </summary>
    [ClaudeTool("list_workbooks",
        "List all Excel files in the output directory.",
        Categories = ["excel", "file"])]
    public string ListWorkbooks()
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
                return "No Excel files found in output directory.";

            var result = string.Join("\n", files.Select(f =>
                $"- {f.name} ({f.size:N0} bytes, modified {f.modified:g})"));

            return $"Excel files:\n{result}";
        }
        catch (Exception ex)
        {
            return $"Failed to list workbooks: {ex.Message}";
        }
    }

    /// <summary>
    /// Read the contents of an Excel workbook for inspection.
    /// </summary>
    [ClaudeTool("read_workbook",
        "Read the contents of an Excel workbook for inspection.",
        Categories = ["excel", "file"],
        TimeoutSeconds = 10)]
    public string ReadWorkbook(
        [ToolParameter(Description = "Name of the Excel file")] string fileName,
        [ToolParameter(Description = "Maximum number of rows to read")] int? maxRows = 20,
        [ToolParameter(Description = "Maximum number of columns to read")] int? maxColumns = 10)
    {
        try
        {
            var filePath = ResolveFilePath(fileName);
            if (!File.Exists(filePath))
                return $"Workbook not found: {filePath}";

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

                var actualMaxRows = Math.Min(maxRows ?? 20, usedRange.RowCount());
                var actualMaxCols = Math.Min(maxColumns ?? 10, usedRange.ColumnCount());

                // Build a simple table representation
                for (int row = 1; row <= actualMaxRows; row++)
                {
                    var cells = new List<string>();
                    for (int col = 1; col <= actualMaxCols; col++)
                    {
                        var cell = usedRange.Cell(row, col);
                        var value = cell.HasFormula
                            ? $"={cell.FormulaA1}"
                            : cell.GetString();
                        cells.Add(value.Length > 20 ? value[..17] + "..." : value);
                    }
                    results.Add($"| {string.Join(" | ", cells)} |");
                }

                if (usedRange.RowCount() > actualMaxRows || usedRange.ColumnCount() > actualMaxCols)
                {
                    results.Add($"... (showing {actualMaxRows}x{actualMaxCols} of {usedRange.RowCount()}x{usedRange.ColumnCount()})");
                }
            }

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            return $"Failed to read workbook: {ex.Message}";
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

// Input types for Excel tools - marked with [GenerateSchema] for compile-time schema generation

/// <summary>Input for creating a workbook.</summary>
[GenerateSchema]
public record CreateWorkbookInput
{
    [ToolParameter(Description = "Name of the Excel file to create")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Optional array of sheet names to create")]
    public string[]? Sheets { get; init; }
}

/// <summary>Input for adding data to a workbook.</summary>
[GenerateSchema]
public record AddDataInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Name of the sheet (optional)")]
    public string? SheetName { get; init; }

    [ToolParameter(Description = "Starting row number (1-based)")]
    public int? StartRow { get; init; }

    [ToolParameter(Description = "Starting column number (1-based)")]
    public int? StartColumn { get; init; }

    [ToolParameter(Description = "Column headers")]
    public string[]? Headers { get; init; }

    [ToolParameter(Description = "Data rows as 2D array")]
    public object[][]? Rows { get; init; }
}

/// <summary>Input for formatting a range.</summary>
[GenerateSchema]
public record FormatRangeInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Name of the sheet")]
    public string? SheetName { get; init; }

    [ToolParameter(Description = "Range to format (e.g., 'A1:D10')")]
    public required string Range { get; init; }

    [ToolParameter(Description = "Apply bold formatting")]
    public bool? Bold { get; init; }

    [ToolParameter(Description = "Apply italic formatting")]
    public bool? Italic { get; init; }

    [ToolParameter(Description = "Font color as HTML color code")]
    public string? FontColor { get; init; }

    [ToolParameter(Description = "Background color as HTML color code")]
    public string? BackgroundColor { get; init; }

    [ToolParameter(Description = "Number format string")]
    public string? NumberFormat { get; init; }

    [ToolParameter(Description = "Horizontal alignment ('left', 'center', 'right')")]
    public string? HorizontalAlignment { get; init; }

    [ToolParameter(Description = "Add borders to the range")]
    public bool? AddBorder { get; init; }
}

/// <summary>Input for adding a formula.</summary>
[GenerateSchema]
public record AddFormulaInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Name of the sheet")]
    public string? SheetName { get; init; }

    [ToolParameter(Description = "Cell address (e.g., 'A1')")]
    public required string Cell { get; init; }

    [ToolParameter(Description = "Excel formula (e.g., '=SUM(A1:A10)')")]
    public required string Formula { get; init; }

    [ToolParameter(Description = "Number format for the result")]
    public string? NumberFormat { get; init; }
}

/// <summary>Input for creating a chart.</summary>
[GenerateSchema]
public record CreateChartInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Name of the sheet")]
    public string? SheetName { get; init; }

    [ToolParameter(Description = "Data range for the chart")]
    public required string DataRange { get; init; }

    [ToolParameter(Description = "Chart type (e.g., 'Bar', 'Line', 'Pie')")]
    public required string ChartType { get; init; }

    [ToolParameter(Description = "Chart title")]
    public string? Title { get; init; }
}

/// <summary>Input for listing workbooks.</summary>
[GenerateSchema]
public record ListWorkbooksInput;

/// <summary>Input for reading a workbook.</summary>
[GenerateSchema]
public record ReadWorkbookInput
{
    [ToolParameter(Description = "Name of the Excel file")]
    public required string FileName { get; init; }

    [ToolParameter(Description = "Maximum number of rows to read")]
    public int? MaxRows { get; init; }

    [ToolParameter(Description = "Maximum number of columns to read")]
    public int? MaxColumns { get; init; }
}
