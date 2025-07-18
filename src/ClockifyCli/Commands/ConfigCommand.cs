using Spectre.Console.Cli;

namespace ClockifyCli.Commands;

public abstract class ConfigCommand : AsyncCommand
{
    public override abstract Task<int> ExecuteAsync(CommandContext context);
}