using System.CommandLine;
using Spectre.Console;

namespace OmniFlow.Tools.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("OmniFlow CLI - Saga inspection and management tool");

        // List sagas command
        var listCommand = new Command("list", "List all active sagas")
        {
            new Option<string>("--connection", "Database connection string") { IsRequired = true }
        };
        listCommand.SetHandler(ListSagasAsync);

        // Inspect saga command
        var inspectCommand = new Command("inspect", "Inspect saga details")
        {
            new Option<string>("--saga-id", "Saga ID to inspect") { IsRequired = true },
            new Option<string>("--connection", "Database connection string") { IsRequired = true }
        };
        inspectCommand.SetHandler(InspectSagaAsync);

        // Replay saga command
        var replayCommand = new Command("replay", "Replay a failed saga")
        {
            new Option<string>("--saga-id", "Saga ID to replay") { IsRequired = true },
            new Option<string>("--connection", "Database connection string") { IsRequired = true }
        };
        replayCommand.SetHandler(ReplaySagaAsync);

        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(inspectCommand);
        rootCommand.AddCommand(replayCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static Task ListSagasAsync()
    {
        var table = new Table();
        table.AddColumn("Saga ID");
        table.AddColumn("Type");
        table.AddColumn("Status");
        table.AddColumn("Created");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[yellow]Note: Database integration pending. This is a CLI stub.[/]");
        
        return Task.CompletedTask;
    }

    static Task InspectSagaAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Saga inspection feature - implementation pending[/]");
        return Task.CompletedTask;
    }

    static Task ReplaySagaAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Saga replay feature - implementation pending[/]");
        return Task.CompletedTask;
    }
}
