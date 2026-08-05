using Microsoft.Extensions.Primitives;

namespace MeetingMinutesAI.Api.Meetings;

internal enum EntityTagParseResult
{
    Success,
    Missing,
    Invalid,
}

internal static class EntityTagVersion
{
    private const int SqlServerRowVersionLength = 8;

    public static EntityTagParseResult Parse(
        StringValues headerValues,
        out byte[] version)
    {
        version = [];
        if (StringValues.IsNullOrEmpty(headerValues))
        {
            return EntityTagParseResult.Missing;
        }

        if (headerValues.Count != 1)
        {
            return EntityTagParseResult.Invalid;
        }

        var value = headerValues[0]?.Trim();
        if (value is null
            || value.Length < 2
            || value[0] != '"'
            || value[^1] != '"'
            || value.Contains(','))
        {
            return EntityTagParseResult.Invalid;
        }

        try
        {
            version = Convert.FromBase64String(value[1..^1]);
        }
        catch (FormatException)
        {
            return EntityTagParseResult.Invalid;
        }

        return version.Length == SqlServerRowVersionLength
            ? EntityTagParseResult.Success
            : EntityTagParseResult.Invalid;
    }

    public static string Format(string version) => $"\"{version}\"";
}
