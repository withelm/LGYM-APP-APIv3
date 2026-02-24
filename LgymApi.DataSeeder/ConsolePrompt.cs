namespace LgymApi.DataSeeder;

public static class ConsolePrompt
{
    public static bool Confirm(string prompt, bool defaultValue)
    {
        var suffix = defaultValue ? "[Y/n]" : "[y/N]";
        while (true)
        {
            Console.Write($"{prompt} {suffix}: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            var trimmed = input.Trim();
            if (trimmed.Equals("y", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.Equals("n", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Console.WriteLine("Please answer with 'y' or 'n'.");
        }
    }

    public static string Choose(string prompt, IReadOnlyList<string> options, string defaultValue)
    {
        if (options.Count == 0)
        {
            throw new ArgumentException("Options collection cannot be empty.", nameof(options));
        }

        var normalizedOptions = options.Select(option => option.Trim()).ToList();
        while (true)
        {
            var optionsText = string.Join("/", normalizedOptions);
            Console.Write($"{prompt} [{optionsText}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            var trimmed = input.Trim();
            var match = normalizedOptions.FirstOrDefault(option => option.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            Console.WriteLine($"Please choose one of: {optionsText}.");
        }
    }
}
