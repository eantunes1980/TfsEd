using System.Diagnostics;

namespace TfsEd.Core.Credentials;

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Runs helper tools such as <c>security</c> or <c>secret-tool</c>.</summary>
internal static class ProcessRunner
{
    public static ProcessResult Run(string fileName, IEnumerable<string> arguments, string? standardInput = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new CredentialStoreException($"Could not start '{fileName}'.");
        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error);
    }

    public static bool IsOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, executable)));
    }
}
