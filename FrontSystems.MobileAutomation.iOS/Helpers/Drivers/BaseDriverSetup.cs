using FrontSystems.MobileAutomation.iOS.Config;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Helpers.Drivers;

public abstract class BaseDriverSetup
{
    public string PlatformName { get; protected set; } = string.Empty;
    public string DeviceName { get; protected set; } = string.Empty;
    public string OSVersion { get; protected set; } = string.Empty;

    public abstract AppiumOptions GetCapabilities();
    public abstract Uri GetAppiumServerUri();

    /// <summary>
    /// True when running under the BrowserStack SDK — CI sets BS_USE_SDK=true and runs via
    /// `dotnet browserstack-sdk &lt;dll&gt;`. In SDK mode the SDK injects device/app/creds/bstack:options
    /// from browserstack.yml and runs a local hub on 127.0.0.1:4723. When false, the driver setups use
    /// either the local path (no BS_APP_HASH) or the legacy manual-capabilities BrowserStack path
    /// (hub-cloud + bstack:options, kept for the AI-authoring job, FRO-14449). Shared here so the env-var
    /// name/semantics stay consistent across platforms.
    /// </summary>
    protected static bool UseBrowserStackSdk =>
        string.Equals(Environment.GetEnvironmentVariable("BS_USE_SDK"), "true", StringComparison.OrdinalIgnoreCase);

    protected static string GetAppHash()
    {
        return BrowserStackConfigReader.GetAppHash();
    }

    protected static string GetBrowserStackUsername()
    {
        return BrowserStackConfigReader.GetUsername();
    }

    protected static string GetBrowserStackAccessKey()
    {
        return BrowserStackConfigReader.GetAccessKey();
    }
}
