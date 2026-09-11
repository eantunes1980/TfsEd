using System.CommandLine;
using TfsEd.Cli.Commands;

namespace TfsEd.Cli;

public static class CliApp
{
    public static RootCommand Create(CliServices services)
    {
        var root = new RootCommand("TfsEd - cross-platform command-line client for TFVC on Azure DevOps Server / TFS.");
        root.Options.Add(GlobalOptions.Collection);
        root.Subcommands.Add(LoginCommand.Create(services));
        root.Subcommands.Add(LogoutCommand.Create(services));
        root.Subcommands.Add(InfoCommand.Create(services));
        return root;
    }
}

internal static class GlobalOptions
{
    public static readonly Option<string?> Collection = new("--collection", "-c")
    {
        Description = "Collection URL. Defaults to TFSED_COLLECTION or the collection of the last login.",
        Recursive = true,
    };
}
