using System.Text;

namespace TfsEd.Cli;

internal static class SecretPrompt
{
    /// <summary>Reads a secret without echo on a terminal, or one line from redirected stdin.</summary>
    public static string Read(string prompt, CliServices services)
    {
        if (!services.IsInteractive)
        {
            return services.Input.ReadLine() ?? string.Empty;
        }

        services.Error.Write(prompt);
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }

        services.Error.WriteLine();
        return buffer.ToString();
    }
}
