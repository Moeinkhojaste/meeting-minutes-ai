using MeetingMinutesAI.Api.Configuration;

namespace MeetingMinutesAI.Api.Tests;

public sealed class DotEnvLoaderTests
{
    [Fact]
    public void ExistingEnvironmentValueWinsOverDotEnv()
    {
        const string name = "MM_TEST_CONFIG_PRECEDENCE";
        var previous = Environment.GetEnvironmentVariable(name);
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, $"{name}=from-dot-env");
            Environment.SetEnvironmentVariable(name, "from-environment");

            DotEnvLoader.LoadWithoutOverwritingEnvironment(path);

            Assert.Equal(
                "from-environment",
                Environment.GetEnvironmentVariable(name)
            );
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
            File.Delete(path);
        }
    }
}
