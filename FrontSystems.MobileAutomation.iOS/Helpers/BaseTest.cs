using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace FrontSystems.MobileAutomation.iOS.Helpers;

public class BaseTest
{
    protected AppiumDriver Driver { get; set; } = null!;

    [SetUp]
    public virtual void SetUp()
    {
    }

    [TearDown]
    public virtual void TearDown()
    {
        try
        {
            bool isRunningOnBrowserStack = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BS_APP_HASH"));

            if (isRunningOnBrowserStack && Driver != null)
            {
                string status = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed ? "passed" : "failed";
                string reason = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed
                    ? $"Test '{TestContext.CurrentContext.Test.Name}' passed"
                    : $"Test '{TestContext.CurrentContext.Test.Name}' failed";

                // Own try/catch: if setSessionStatus throws (e.g. the session is already invalid after
                // an app crash), the failure diagnostics below must still run.
                try
                {
                    // Serialize the payload so status/reason are properly escaped — TestName can contain
                    // quotes/newlines, which would otherwise produce invalid JSON via string concatenation.
                    var executorPayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        action = "setSessionStatus",
                        arguments = new { status, reason }
                    });
                    Driver.ExecuteScript($"browserstack_executor: {executorPayload}");

                    Console.WriteLine($"✅ BrowserStack status set to: {status}");
                }
                catch (Exception statusEx)
                {
                    Console.WriteLine($"Failed to set BrowserStack status: {statusEx.Message}");
                }

                if (status == "failed")
                {
                    // Surface the BrowserStack session details on failure — the returned JSON includes
                    // the device-log / Appium-log URLs and the dashboard link, where the native crash
                    // log lives. This makes app crashes diagnosable straight from the test output.
                    try
                    {
                        var details = Driver.ExecuteScript("browserstack_executor: {\"action\": \"getSessionDetails\"}");
                        Console.WriteLine($"🔎 BrowserStack session details (device/crash logs): {details}");
                    }
                    catch (Exception detailsEx)
                    {
                        Console.WriteLine($"Could not fetch BrowserStack session details: {detailsEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set BrowserStack status: {ex.Message}");
        }
        finally
        {
            DriverFactory.QuitDriver();
        }
    }

    protected AppiumElement? FindElementWithTimeout(By locator, int timeoutSeconds)
    {
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(Driver, TimeSpan.Zero);
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(_ =>
            {
                try
                {
                    var element = Driver.FindElement(locator);
                    return element?.Displayed == true ? element : null;
                }
                catch (NoSuchElementException) { return null; }
                catch (StaleElementReferenceException) { return null; }
                catch (InvalidSelectorException) { return null; }
            });
        }
        catch (WebDriverTimeoutException) { return null; }
        finally
        {
            DriverFactory.SetImplicitWait(Driver, originalImplicitWait);
        }
    }

    protected void TakeScreenshot(string stepName)
    {
        try
        {
            var screenshotsDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(screenshotsDir);
            var screenshot = ((ITakesScreenshot)Driver).GetScreenshot();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            screenshot.SaveAsFile(Path.Combine(screenshotsDir, $"{stepName}_{timestamp}.png"));
        }
        catch { }
    }

    protected void TapCenterScreen()
    {
        try
        {
            var size = Driver.Manage().Window.Size;
            var actions = new OpenQA.Selenium.Interactions.Actions(Driver);
            actions.MoveToLocation(size.Width / 2, size.Height / 2).Click().Perform();
        }
        catch { }
    }

    /// <summary>
    /// Taps a point given as a fraction of the current window size (0.0-1.0 on each axis).
    /// For icon-only controls with no accessible name/label that FindElementWithTimeout can
    /// never locate — e.g. a close "X" glyph. Fraction-based rather than raw pixels so the tap
    /// still lands correctly if window size differs from whatever device the fraction was
    /// measured on.
    /// </summary>
    protected void TapAtFraction(double xFraction, double yFraction)
    {
        if (xFraction < 0.0 || xFraction > 1.0 || yFraction < 0.0 || yFraction > 1.0)
            throw new ArgumentOutOfRangeException(
                xFraction < 0.0 || xFraction > 1.0 ? nameof(xFraction) : nameof(yFraction),
                "Fractions must be between 0.0 and 1.0.");

        try
        {
            var size = Driver.Manage().Window.Size;
            // Clamp to Width-1/Height-1: a fraction of exactly 1.0 (or a value rounded up to
            // it) multiplies out to Width/Height, which is one pixel past the valid range and
            // can land off-screen. Confirmed via Copilot review, 2026-09-04.
            var x = Math.Min((int)(size.Width * xFraction), size.Width - 1);
            var y = Math.Min((int)(size.Height * yFraction), size.Height - 1);
            var actions = new OpenQA.Selenium.Interactions.Actions(Driver);
            actions.MoveToLocation(x, y).Click().Perform();
        }
        catch { }
    }
}
