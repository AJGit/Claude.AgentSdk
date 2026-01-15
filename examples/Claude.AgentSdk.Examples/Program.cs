using Claude.AgentSdk.Examples.Examples;

namespace Claude.AgentSdk.Examples;

/// <summary>
/// Main entry point for the Claude Agent SDK examples.
/// Run with: dotnet run -- [example-number]
/// Or run without arguments to see the menu.
/// </summary>
public static class Program
{
    private static readonly IExample[] Examples =
    [
        new BasicQueryExample(),
        new StreamingExample(),
        new InteractiveSessionExample(),
        new CustomToolsExample(),
        new HooksExample(),
        new SubagentsExample(),
        new StructuredOutputExample(),
        new PermissionHandlerExample(),
        new SystemPromptExample(),
        new SettingsSourcesExample(),
        new McpServersExample(),
    ];

    public static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   Claude Agent SDK - C# Examples");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // If an argument is provided, run that specific example
        if (args.Length > 0 && int.TryParse(args[0], out var exampleNumber))
        {
            if (exampleNumber >= 1 && exampleNumber <= Examples.Length)
            {
                await RunExampleAsync(Examples[exampleNumber - 1]);
                return;
            }
            Console.WriteLine($"Invalid example number: {exampleNumber}");
            Console.WriteLine();
        }

        // Show menu
        while (true)
        {
            ShowMenu();

            Console.Write("Enter your choice (or 'q' to quit): ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) || input.Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= Examples.Length)
            {
                Console.WriteLine();
                await RunExampleAsync(Examples[choice - 1]);
                Console.WriteLine();
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
                Console.Clear();
            }
            else
            {
                Console.WriteLine("Invalid choice. Please try again.");
                Console.WriteLine();
            }
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("Available Examples:");
        Console.WriteLine("-------------------");

        for (var i = 0; i < Examples.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {Examples[i].Name}");
            Console.WriteLine($"     {Examples[i].Description}");
            Console.WriteLine();
        }
    }

    private static async Task RunExampleAsync(IExample example)
    {
        Console.WriteLine($"Running: {example.Name}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();

        try
        {
            await example.RunAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();

            if (ex.InnerException is not null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
    }
}
