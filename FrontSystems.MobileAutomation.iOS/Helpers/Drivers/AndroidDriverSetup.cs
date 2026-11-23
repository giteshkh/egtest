using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Helpers.Drivers;

public class AndroidDriverSetup : BaseDriverSetup
{
    public AndroidDriverSetup()
    {
        PlatformName = "Android";
    }

    public override AppiumOptions GetCapabilities()
    {
        return UseBrowserStackSdk ? GetBrowserStackSdkCapabilities() : GetBrowserStackCapabilities();
    }

    public override Uri GetAppiumServerUri()
    {
        // SDK runs a local hub on 127.0.0.1:4723; the legacy manual path talks to the hub directly.
        return UseBrowserStackSdk
            ? new Uri("http://127.0.0.1:4723")
            : new Uri("https://hub-cloud.browserstack.com/wd/hub");
    }

    /// <summary>Minimal caps for the BrowserStack SDK path — SDK injects device/app/creds/bstack:options from browserstack.yml.</summary>
    private static AppiumOptions GetBrowserStackSdkCapabilities()
    {
        Console.WriteLine("Creating Android BrowserStack SDK capabilities (SDK injects device/app/creds from browserstack.yml)...");

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2"
        };
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);

        return options;
    }

    private AppiumOptions GetBrowserStackCapabilities()
    {
        Console.WriteLine("Creating Android BrowserStack capabilities...");

        string deviceName = Environment.GetEnvironmentVariable("BS_DEVICE_NAME") ?? "Samsung Galaxy S25";
        string osVersion = Environment.GetEnvironmentVariable("BS_OS_VERSION") ?? "15.0";
        string orientation = Environment.GetEnvironmentVariable("BS_DEVICE_ORIENTATION") ?? "portrait";

        DeviceName = deviceName;
        OSVersion = osVersion;

        Console.WriteLine($"BrowserStack Device: {deviceName} - Android {osVersion} - {orientation}");

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            PlatformVersion = osVersion,
            DeviceName = deviceName,
            App = GetAppHash(),
            AutomationName = "UiAutomator2"
        };
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);

        var bstackOptions = new Dictionary<string, object>
            {
                {"userName", GetBrowserStackUsername()},
                {"accessKey", GetBrowserStackAccessKey()},
                {"projectName", "Front Systems"},
                {"buildName", Environment.GetEnvironmentVariable("BS_BUILD_NAME") ?? "1"},
                {"sessionName", $"Android Test - {deviceName} ({osVersion})"},
                {"deviceOrientation", orientation},
                {"debug", true},
                {"networkLogs", true}
            };

        options.AddAdditionalAppiumOption("bstack:options", bstackOptions);

        return options;
    }
}
