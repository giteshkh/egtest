using Newtonsoft.Json;

namespace FrontSystems.MobileAutomation.iOS.Config;

public static class BrowserStackConfigReader
{
    public static string GetAppHash()
    {
        string? envValue = Environment.GetEnvironmentVariable("BS_APP_HASH");

        if (!string.IsNullOrEmpty(envValue))
        {
            Console.WriteLine($"Using BrowserStack AppHash from environment variable {envValue}");
            return envValue;
        }

        var config = LoadConfig();
        Console.WriteLine($"Using BrowserStack AppHash from AppConfig.json {config.BrowserStack.AppHash}");
        return config.BrowserStack.AppHash;
    }

    public static string GetUsername()
    {
        string? envValue = Environment.GetEnvironmentVariable("BS_USERNAME")
            ?? Environment.GetEnvironmentVariable("BROWSERSTACK_USERNAME");

        if (!string.IsNullOrEmpty(envValue))
        {
            Console.WriteLine($"Using BrowserStack Username from environment variable {envValue}");
            return envValue;
        }

        var config = LoadConfig();
        Console.WriteLine($"Using BrowserStack Username from AppConfig.json {config.BrowserStack.Username}");
        return config.BrowserStack.Username;
    }

    public static string GetAccessKey()
    {
        string? envValue = Environment.GetEnvironmentVariable("BS_ACCESS_KEY")
            ?? Environment.GetEnvironmentVariable("BROWSERSTACK_ACCESS_KEY");

        if (!string.IsNullOrEmpty(envValue))
        {
            Console.WriteLine("Using BrowserStack AccessKey from environment variable");
            return envValue;
        }

        Console.WriteLine("Using BrowserStack AccessKey from AppConfig.json");
        var config = LoadConfig();
        return config.BrowserStack.AccessKey;
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
        public required BrowserStackConfig BrowserStack { get; set; }
    }

    private class BrowserStackConfig
    {
        public required string AppHash { get; set; }
        public required string Username { get; set; }
        public required string AccessKey { get; set; }
    }
}
