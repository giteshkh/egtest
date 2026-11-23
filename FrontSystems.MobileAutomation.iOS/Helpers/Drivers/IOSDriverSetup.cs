using FrontSystems.MobileAutomation.iOS.Helpers;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Helpers.Drivers;

public class IOSDriverSetup : BaseDriverSetup
{
    private readonly bool _useLocalDevice;
    private readonly bool _useSimulator;

    public IOSDriverSetup(bool useLocalDevice = true, bool useSimulator = false)
    {
        _useLocalDevice = useLocalDevice;
        _useSimulator = useSimulator;
        PlatformName = "iOS";
    }

    public override AppiumOptions GetCapabilities()
    {
        // local (no BS_APP_HASH) → real device; SDK run (BS_USE_SDK=true) → minimal SDK caps;
        // otherwise the legacy manual-capabilities BrowserStack path (kept for AI authoring).
        if (_useLocalDevice)
        {
            return _useSimulator ? GetSimulatorCapabilities() : GetLocalRealDeviceCapabilities();
        }

        return UseBrowserStackSdk ? GetBrowserStackSdkCapabilities() : GetBrowserStackCapabilities();
    }

    public override Uri GetAppiumServerUri()
    {
        // Local Appium and the BrowserStack SDK both use the local hub on 127.0.0.1:4723.
        // The legacy manual-capabilities path (AI authoring) talks to the BrowserStack hub directly.
        if (_useLocalDevice || UseBrowserStackSdk)
        {
            return new Uri("http://127.0.0.1:4723");
        }

        return new Uri("https://hub-cloud.browserstack.com/wd/hub");
    }

    /// <summary>
    /// Forwards every configured test-account credential into the app's own process environment —
    /// the app runs as a separate process from this test runner, so InstallNewPOSViewModel.cs
    /// reading Environment.GetEnvironmentVariable only sees these if they're passed through here.
    /// Keeps every credential out of source entirely (all were previously hardcoded — FRO-16822
    /// fixed France first, FRO-16831 generalized this to Sweden/USMA/Dubai/testautomation/demo).
    ///
    /// Confirmed via Copilot review on FRO-16822, 2026-09-04: this must be called from every
    /// capability path, not just the local real-device one — CI runs via BrowserStack, where
    /// BS_APP_HASH is set and DriverFactory picks a different capabilities method entirely.
    /// Forwarding in only one path silently breaks the others. Each pair is forwarded independently —
    /// a test only needs the env vars for the account it actually uses set.
    /// </summary>
    private static readonly (string User, string Password)[] TestAccountCredentialVars =
    {
        ("FISCAL_FRANCE_TEST_USER", "FISCAL_FRANCE_TEST_PASSWORD"),
        ("FISCAL_SWEDEN_TEST_USER", "FISCAL_SWEDEN_TEST_PASSWORD"),
        ("FISCAL_USMA_TEST_USER", "FISCAL_USMA_TEST_PASSWORD"),
        ("FISCAL_DUBAI_TEST_USER", "FISCAL_DUBAI_TEST_PASSWORD"),
        ("TESTAUTOMATION_FRONTKASSE_USER", "TESTAUTOMATION_FRONTKASSE_PASSWORD"),
        // No DEMO_* pair: the demo account's bypass is intentionally not environment-driven,
        // because it has to work in a shipped App Store build. See InstallNewPOSViewModel.
    };

    private static void AddTestAccountCredentialForwarding(AppiumOptions options)
    {
        var environment = new Dictionary<string, string>();
        foreach (var (userVar, passwordVar) in TestAccountCredentialVars)
        {
            var user = Environment.GetEnvironmentVariable(userVar);
            var password = Environment.GetEnvironmentVariable(passwordVar);
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
                continue;

            environment[userVar] = user;
            environment[passwordVar] = password;
        }

        if (environment.Count == 0)
            return;

        options.AddAdditionalAppiumOption("processArguments", new Dictionary<string, object>
        {
            { "args", new string[] { } },
            // appium-xcuitest-driver only reads capabilities.processArguments.env — "environment"
            // is silently ignored, so this forwarding was a no-op on every platform/device.
            { "env", environment }
        });
    }

    /// <summary>
    /// Minimal capabilities for the BrowserStack SDK path. The SDK injects device, app, credentials
    /// and bstack:options (debug/networkLogs/appProfiling/etc.) from browserstack.yml — so we set
    /// NONE of those here; doing so would conflict with the SDK's injection. processArguments is
    /// unrelated to that injection (it targets the app's own launch environment, not session/device/
    /// app capabilities), so it's still forwarded here.
    /// </summary>
    private static AppiumOptions GetBrowserStackSdkCapabilities()
    {
        Console.WriteLine("Creating iOS BrowserStack SDK capabilities (SDK injects device/app/creds from browserstack.yml)...");

        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            AutomationName = "XCUITest"
        };
        options.AddAdditionalAppiumOption("bundleId", DriverFactory.AppBundleId);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        AddTestAccountCredentialForwarding(options);

        return options;
    }

    private static AppiumOptions GetSimulatorCapabilities()
    {
        Console.WriteLine("Creating iOS Simulator capabilities...");
        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            PlatformVersion = "18.0",
            DeviceName = "iPad Pro 12.9-inch (7th generation)"
        };
        options.AddAdditionalAppiumOption("bundleId", DriverFactory.AppBundleId);
        options.AutomationName = "XCUITest";
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("permissions", new Dictionary<string, object>
            {
                { DriverFactory.AppBundleId, new Dictionary<string, string>
                    {
                        { "camera", "YES" },
                        { "microphone", "YES" },
                        { "location", "YES" }
                    }
                }
            });
        AddTestAccountCredentialForwarding(options);

        return options;
    }

    private static AppiumOptions GetLocalRealDeviceCapabilities()
    {
        Console.WriteLine("Creating iOS Real Device capabilities...");
        // Active UDID is read from env var APPIUM_UDID so any connected device can be targeted
        // without touching this file. Falls back to "Vibeke sin lilla mini 10th" (26.5).
        // Known UDIDs:
        //   Vibeke blue 10th gen (26.6): 00008101-000818261143A01E
        //   Vibeke sin lilla mini 10th (26.5): 00008130-00184DEA010A001C
        //   iPad 7th gen (iOS 18.7.7): 2b9f3335574f8729001b02fe9c3b77eb677c538b
        string udid = Environment.GetEnvironmentVariable("APPIUM_UDID")
                   ?? "00008130-00184DEA010A001C";
        string platformVersion = Environment.GetEnvironmentVariable("APPIUM_IOS_VERSION")
                              ?? "26.5";

        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            PlatformVersion = platformVersion
        };
        options.AddAdditionalAppiumOption("udid", udid);
        options.AddAdditionalAppiumOption("realDevice", true);
        options.AddAdditionalAppiumOption("bundleId", DriverFactory.AppBundleId);
        // Opt-in (APPIUM_KEEP_APP_RUNNING=true): attach to the app that is already running instead of letting
        // Appium terminate and relaunch it. A relaunch drops the Keystone login and the iPad then needs a manual
        // login again before every session, which a test run cannot do (FiscalSwedenEfrTest relies on this).
        if (string.Equals(Environment.GetEnvironmentVariable("APPIUM_KEEP_APP_RUNNING"), "true", StringComparison.OrdinalIgnoreCase))
        {
            options.AddAdditionalAppiumOption("noReset", true);
            options.AddAdditionalAppiumOption("shouldTerminateApp", false);
            options.AddAdditionalAppiumOption("forceAppLaunch", false);
        }
        options.AutomationName = "XCUITest";
        // false: reuse the already-installed, already-trusted WDA runner instead of
        // uninstalling/rebuilding/re-signing it every session (which re-triggers the
        // "Developer App Certificate is not trusted" prompt on every run).
        options.AddAdditionalAppiumOption("useNewWDA", false);
        options.AddAdditionalAppiumOption("showXcodeLog", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 900);
        // Needed for WDA's own xctrunner bundle to get an auto-generated provisioning
        // profile on this machine's personal dev team (no profile is checked in).
        options.AddAdditionalAppiumOption("allowProvisioningDeviceRegistration", true);
        AddTestAccountCredentialForwarding(options);

        Console.WriteLine("⚠️ For real device: Manually grant camera permission in Settings → Privacy → Camera");

        return options;
    }

    private AppiumOptions GetBrowserStackCapabilities()
    {
        Console.WriteLine("Creating iOS BrowserStack capabilities...");

        string deviceName = Environment.GetEnvironmentVariable("BS_DEVICE_NAME") ?? "iPad 10th";
        string osVersion = Environment.GetEnvironmentVariable("BS_OS_VERSION") ?? "16";
        string orientation = Environment.GetEnvironmentVariable("BS_DEVICE_ORIENTATION") ?? "portrait";

        DeviceName = deviceName;
        OSVersion = osVersion;

        Console.WriteLine($"BrowserStack Device: {deviceName} - iOS {osVersion} - {orientation}");

        string buildName = Environment.GetEnvironmentVariable("BS_BUILD_NAME") ?? "1";
        Console.WriteLine($"BrowserStack Build Name: {buildName}");

        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            PlatformVersion = osVersion,
            DeviceName = deviceName,
            AutomationName = "XCUITest",
            App = GetAppHash()
        };
        options.AddAdditionalAppiumOption("bundleId", DriverFactory.AppBundleId);
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("shouldTerminateApp", true);
        options.AddAdditionalAppiumOption("snapshotMaxDepth", 62);

        var bstackOptions = new Dictionary<string, object>
            {
                {"userName", GetBrowserStackUsername()},
                {"accessKey", GetBrowserStackAccessKey()},
                {"projectName", "Front Systems"},
                {"buildName", buildName},
                {"sessionName", $"iOS Test - {deviceName} ({osVersion})"},
                {"deviceOrientation", orientation},
                {"debug", true},
                {"networkLogs", true},
                {"autoAcceptAlerts", true},
                {"interactiveDebugging", false}
            };

        // Cross-Device Automation Agent (AI authoring). Opt-in per run via BS_AI_AUTHORING=true.
        // Additive and inert otherwise — runs on this manual capabilities path, no BrowserStack SDK.
        if (string.Equals(Environment.GetEnvironmentVariable("BS_AI_AUTHORING"), "true", StringComparison.OrdinalIgnoreCase))
        {
            bstackOptions["aiAuthoring"] = true;
            Console.WriteLine("BrowserStack AI authoring (Cross-Device Automation Agent) enabled.");
        }

        options.AddAdditionalAppiumOption("bstack:options", bstackOptions);
        AddTestAccountCredentialForwarding(options);

        return options;
    }
}
