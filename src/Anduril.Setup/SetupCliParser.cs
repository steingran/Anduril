namespace Anduril.Setup;

internal static class SetupCliParser
{
    public static bool TryParse(
        string[] args,
        Func<string, string?> getEnvironmentVariable,
        out SetupCliOptions? options,
        out string? errorMessage)
    {
        var nonInteractive = ReadEnvironmentFlag(getEnvironmentVariable("ANDURIL_SETUP_NON_INTERACTIVE"));
        var showHelp = false;
        var configPath = Normalize(getEnvironmentVariable("ANDURIL_SETUP_CONFIG_PATH"));
        var provider = Normalize(getEnvironmentVariable("ANDURIL_SETUP_PROVIDER"));
        var model = Normalize(getEnvironmentVariable("ANDURIL_SETUP_MODEL"));
        var apiKey = Normalize(getEnvironmentVariable("ANDURIL_SETUP_API_KEY"));
        var endpoint = Normalize(getEnvironmentVariable("ANDURIL_SETUP_ENDPOINT"));
        var positionalConfigConsumed = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("-", StringComparison.Ordinal))
            {
                if (positionalConfigConsumed)
                {
                    options = null;
                    errorMessage = $"Unexpected positional argument '{arg}'.";
                    return false;
                }

                configPath = Normalize(arg);
                positionalConfigConsumed = true;
                continue;
            }

            ParseOption(arg, out var optionName, out var inlineValue);
            switch (optionName)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--non-interactive":
                case "--noninteractive":
                    if (inlineValue is null)
                    {
                        nonInteractive = true;
                        break;
                    }

                    if (!TryParseBoolean(inlineValue, out var parsedFlag))
                    {
                        options = null;
                        errorMessage = $"Invalid boolean value '{inlineValue}' for {optionName}.";
                        return false;
                    }

                    nonInteractive = parsedFlag;
                    break;
                case "--config":
                case "--config-path":
                    if (!TryReadOptionValue(args, ref i, optionName, inlineValue, out configPath, out errorMessage))
                    {
                        options = null;
                        return false;
                    }
                    break;
                case "--provider":
                    if (!TryReadOptionValue(args, ref i, optionName, inlineValue, out provider, out errorMessage))
                    {
                        options = null;
                        return false;
                    }
                    break;
                case "--model":
                    if (!TryReadOptionValue(args, ref i, optionName, inlineValue, out model, out errorMessage))
                    {
                        options = null;
                        return false;
                    }
                    break;
                case "--api-key":
                    if (!TryReadOptionValue(args, ref i, optionName, inlineValue, out apiKey, out errorMessage))
                    {
                        options = null;
                        return false;
                    }
                    break;
                case "--endpoint":
                    if (!TryReadOptionValue(args, ref i, optionName, inlineValue, out endpoint, out errorMessage))
                    {
                        options = null;
                        return false;
                    }
                    break;
                default:
                    options = null;
                    errorMessage = $"Unknown option '{arg}'.";
                    return false;
            }
        }

        options = new SetupCliOptions
        {
            NonInteractive = nonInteractive,
            ShowHelp = showHelp,
            ConfigPath = configPath,
            Provider = provider,
            Model = model,
            ApiKey = apiKey,
            Endpoint = endpoint
        };
        errorMessage = null;
        return true;
    }

    private static bool ReadEnvironmentFlag(string? value)
        => TryParseBoolean(value, out var result) && result;

    private static void ParseOption(string arg, out string optionName, out string? inlineValue)
    {
        var separatorIndex = arg.IndexOf('=');
        if (separatorIndex < 0)
        {
            optionName = arg;
            inlineValue = null;
            return;
        }

        optionName = arg[..separatorIndex];
        inlineValue = Normalize(arg[(separatorIndex + 1)..]);
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string optionName,
        string? inlineValue,
        out string? value,
        out string? errorMessage)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
            errorMessage = null;
            return true;
        }

        if (index + 1 >= args.Length)
        {
            value = null;
            errorMessage = $"Missing value for {optionName}.";
            return false;
        }

        index++;
        value = Normalize(args[index]);
        errorMessage = null;
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseBoolean(string? value, out bool result)
    {
        switch (Normalize(value)?.ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                result = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }
}