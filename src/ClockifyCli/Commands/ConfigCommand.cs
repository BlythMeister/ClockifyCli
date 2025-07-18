using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public abstract class ConfigCommand : AsyncCommand
{
    public abstract override Task<int> ExecuteAsync(CommandContext context);
}
