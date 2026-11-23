using Newtonsoft.Json;

namespace FrontSystems.MobileAutomation.iOS.Config;

public static class TestCredentialsReader
{
    public static (string username, string password) GetPrimaryCredentials()
    {
        string? envUser = Environment.GetEnvironmentVariable("TEST_PRIMARY_USERNAME");
        string? envPass = Environment.GetEnvironmentVariable("TEST_PRIMARY_PASSWORD");

        if (!string.IsNullOrEmpty(envUser) && !string.IsNullOrEmpty(envPass))
        {
            Console.WriteLine("Using primary credentials from environment variables");
            return (envUser, envPass);
        }

        Console.WriteLine("Using primary credentials from AppConfig.json");
        var config = LoadConfig();
        return (config.TestCredentials.PrimaryUsername, config.TestCredentials.PrimaryPassword);
    }

    public static (string username, string password) GetSecondaryCredentials()
    {
        string? envUser = Environment.GetEnvironmentVariable("TEST_SECONDARY_USERNAME");
        string? envPass = Environment.GetEnvironmentVariable("TEST_SECONDARY_PASSWORD");

        if (!string.IsNullOrEmpty(envUser) && !string.IsNullOrEmpty(envPass))
        {
            Console.WriteLine("Using secondary credentials from environment variables");
            return (envUser, envPass);
        }

        Console.WriteLine("Using secondary credentials from AppConfig.json");
        var config = LoadConfig();
        return (config.TestCredentials.SecondaryUsername, config.TestCredentials.SecondaryPassword);
    }

    private static ConfigModel LoadConfig()
    {
        string configPath = Path.Combine(Directory.GetCurrentDirectory(), "Config", "AppConfig.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"AppConfig.json not found at {configPath}");
        }

        string configContent = File.ReadAllText(configPath);
        var config = JsonConvert.DeserializeObject<ConfigModel>(configContent) ?? throw new InvalidOperationException("Failed to deserialize AppConfig.json");
        return config;
    }

    private class ConfigModel
    {
        public required TestCredentialsConfig TestCredentials { get; set; }
    }

    private class TestCredentialsConfig
    {
        public required string PrimaryUsername { get; set; }
        public required string PrimaryPassword { get; set; }
        public required string SecondaryUsername { get; set; }
        public required string SecondaryPassword { get; set; }
    }
}
