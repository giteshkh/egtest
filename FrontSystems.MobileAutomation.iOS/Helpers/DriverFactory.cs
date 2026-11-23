using FrontSystems.MobileAutomation.iOS.Helpers.Drivers;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace FrontSystems.MobileAutomation.iOS.Helpers;

public static class DriverFactory
{
    /// <summary>Bundle id of the POS app under test. Single source of truth — reused by the iOS
    /// capability setup and the app-state/crash checks so the value can't drift.</summary>
    public static readonly string AppBundleId =
        Environment.GetEnvironmentVariable("APPIUM_BUNDLE_ID") ?? "no.frontsystems.cornerstone"; // override for the .stage app on the QA Sweden iPad

    /// <summary>Android application id of the POS app under test (FRO-16728 follow-up: needed
    /// to explicitly re-foreground the app on BrowserStack — see ActivateApp calls below).</summary>
    public const string AndroidPackageId = "com.eg.dk.frontsystems";

    private static readonly AsyncLocal<AppiumDriver> _driver = new();

    // Configuration - check environment variable for platform
    // Default: iOS
    private static string GetPlatform()
    {
        return Environment.GetEnvironmentVariable("TEST_PLATFORM") ?? "iOS";
    }

    // Detect Android from the driver's runtime type, NOT driver.PlatformName: PlatformName routes
    // through Appium's SessionDetails (GetSession), which BrowserStack returns as a String — not a
    // W3C dictionary — throwing InvalidCastException on .NET 10. The type check also stays correct
    // when an AndroidDriver is created directly (TEST_PLATFORM unset), unlike an env-var check.
    public static bool IsAndroidDriver(AppiumDriver driver) => driver is AndroidDriver;

    // Tracks the implicit wait currently applied to the active driver, per async context to match
    // _driver (so parallel iOS/Android runs don't clobber each other's value). Helpers read this
    // instead of driver.Manage().Timeouts().ImplicitWait — that GET command deserializes as a
    // String (not a Dictionary) on .NET 10 and throws InvalidCastException.
    private static readonly AsyncLocal<TimeSpan?> _implicitWait = new();

    public static TimeSpan CurrentImplicitWait => _implicitWait.Value ?? TimeSpan.FromSeconds(10);

    // Set the implicit wait on the driver AND track it (no GET). To override temporarily, capture
    // CurrentImplicitWait first, then SetImplicitWait(driver, originalValue) to restore. Because the
    // captured value is whatever was current, nested overrides compose correctly: an inner helper
    // restores to the outer override (e.g. TimeSpan.Zero) rather than the global default.
    public static void SetImplicitWait(AppiumDriver driver, TimeSpan value)
    {
        // Apply to the driver first; only update the tracked value on success. A failed SET
        // (e.g. session ended/disposed) then leaves _implicitWait in sync with the driver
        // rather than masking the failure with a stale tracked value.
        driver.Manage().Timeouts().ImplicitWait = value;
        _implicitWait.Value = value;
    }

    // Auto-detect: Use BrowserStack if BS_APP_HASH exists (CI/CD), otherwise use local Appium
    private static readonly bool USE_LOCAL_DEVICE = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BS_APP_HASH"));
    private static readonly bool USE_SIMULATOR = false;

    public static AppiumDriver CreateDriver()
    {
        string platform = GetPlatform();

        if (platform.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            return CreateAndroidDriver();
        }
        else
        {
            return CreateiOSDriver();
        }
    }

    public static IOSDriver CreateiOSDriver()
    {
        Console.WriteLine("Creating iOS Driver...");
        var setup = new IOSDriverSetup(USE_LOCAL_DEVICE, USE_SIMULATOR);
        var capabilities = setup.GetCapabilities();
        var serverUri = setup.GetAppiumServerUri();

        var driver = new IOSDriver(serverUri, capabilities, TimeSpan.FromSeconds(900));
        _driver.Value = driver;
        SetImplicitWait(driver, TimeSpan.FromSeconds(10));

        if (!USE_LOCAL_DEVICE)
        {
            Console.WriteLine($"BrowserStack session ID: {driver.SessionId}");
            Console.WriteLine($"Direct link: https://app-automate.browserstack.com/dashboard/v2/sessions/{driver.SessionId}");
            Console.WriteLine("BrowserStack session started, WDA controls app launch.");
            Thread.Sleep(5000); // Brief wait for app to fully launch under WDA
            ActivateAppSafely(driver, AppBundleId); // FRO-16728 follow-up: WDA sessions can hand
            // back control while the accessibility tree is still SpringBoard's, not the app's.
            // Force-foreground the app explicitly rather than trusting the session start alone.
        }
        else
        {
            Thread.Sleep(15000); // Allow time for local device
        }

        return driver;
    }

    public static AndroidDriver CreateAndroidDriver()
    {
        Console.WriteLine("Creating Android Driver...");
        var setup = new AndroidDriverSetup();
        var capabilities = setup.GetCapabilities();
        var serverUri = setup.GetAppiumServerUri();

        var driver = new AndroidDriver(serverUri, capabilities, TimeSpan.FromSeconds(120));
        _driver.Value = driver;
        SetImplicitWait(driver, TimeSpan.FromSeconds(15));
        Console.WriteLine($"BrowserStack session ID: {driver.SessionId}");
        Console.WriteLine($"Direct link: https://app-automate.browserstack.com/dashboard/v2/sessions/{driver.SessionId}");
        Console.WriteLine("Waiting for Android app to fully launch...");
        Thread.Sleep(10000);
        ActivateAppSafely(driver, AndroidPackageId); // FRO-16728 follow-up — see iOS comment above.
        return driver;
    }

    /// <summary>
    /// Explicitly foregrounds the app under test. FRO-16728 follow-up: on fresh BrowserStack
    /// device sessions we observed the accessibility tree still describing the home screen
    /// (SpringBoard on iOS; an empty tree on Android) well after the session reported ready —
    /// a session-attach race, not a locator problem. ActivateApp is best-effort: if it fails we
    /// log and continue, since the subsequent load-check in LoginHelper is the real backstop
    /// (there is no retry here — only a wait-and-check).
    /// </summary>
    private static void ActivateAppSafely(AppiumDriver driver, string appId)
    {
        try
        {
            driver.ActivateApp(appId);
            Console.WriteLine($"Activated app '{appId}' in foreground.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ActivateApp('{appId}') failed (non-fatal): {ex.Message}");
        }
    }

    public static void QuitDriver()
    {
        if (_driver.Value != null)
        {
            _driver.Value.Quit();
            _driver.Value.Dispose();
            _driver.Value = null!;
        }
    }

    public static AppiumDriver GetDriver()
    {
        return _driver.Value!;
    }

    /// <summary>
    /// Creates an iOS driver with fullReset=true so the app wipes its data and
    /// re-runs onboarding on next launch. Used by PerformanceTest to start fresh.
    ///
    /// Local:        Set PERFORMANCE_TEST_IPA_PATH=/path/to/your.ipa
    /// BrowserStack: Reuses BS_APP_HASH (same build, fresh install via fullReset).
    /// </summary>
    public static IOSDriver CreateDriverWithFullReset()
    {
        Console.WriteLine("Creating iOS Driver with fullReset=true...");
        var setup = new IOSDriverSetup(USE_LOCAL_DEVICE, USE_SIMULATOR);
        var capabilities = setup.GetCapabilities();

        // Force a clean install so the app starts onboarding from scratch
        capabilities.AddAdditionalAppiumOption("fullReset", true);
        capabilities.AddAdditionalAppiumOption("noReset", false);

        if (USE_LOCAL_DEVICE)
        {
            string? ipaPath = Environment.GetEnvironmentVariable("PERFORMANCE_TEST_IPA_PATH");
            if (!string.IsNullOrEmpty(ipaPath))
            {
                Console.WriteLine($"Using local IPA for full reset: {ipaPath}");
                capabilities.App = ipaPath;
            }
            else
            {
                Console.WriteLine("⚠️  PERFORMANCE_TEST_IPA_PATH not set.");
                Console.WriteLine("    export PERFORMANCE_TEST_IPA_PATH=/path/to/FrontSystems.ipa");
            }
        }
        else
        {
            // BrowserStack: reuse the normal app hash — fullReset handles the clean install
            string? appHash = Environment.GetEnvironmentVariable("BS_APP_HASH");
            if (!string.IsNullOrEmpty(appHash))
            {
                capabilities.App = appHash;
                Console.WriteLine($"BrowserStack full-reset app hash: {appHash}");
            }
        }

        var serverUri = setup.GetAppiumServerUri();
        var driver = new IOSDriver(serverUri, capabilities, TimeSpan.FromSeconds(120));
        _driver.Value = driver;
        SetImplicitWait(driver, TimeSpan.FromSeconds(10));

        if (!USE_LOCAL_DEVICE)
        {
            Console.WriteLine($"BrowserStack session ID: {driver.SessionId}");
            Console.WriteLine($"Direct link: https://app-automate.browserstack.com/dashboard/v2/sessions/{driver.SessionId}");
            Thread.Sleep(8000); // Extra time for reinstall + app launch
            ActivateAppSafely(driver, AppBundleId); // Same session-attach race as CreateiOSDriver —
            // PerformanceTest uses this path and hit the SpringBoard/blank-session symptom too.
        }
        else
        {
            Thread.Sleep(20000); // Reinstall takes longer on real device
        }

        return driver;
    }
}
