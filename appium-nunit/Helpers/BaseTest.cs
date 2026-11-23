using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Support.UI;

namespace BStackAppiumNUnit.Helpers;

/// <summary>
/// Creates one Appium iOS session per test and reports pass/fail to BrowserStack.
/// The BrowserStack SDK injects device, OS, app, credentials and bstack:options from browserstack.yml,
/// so no device or app capability is set here (same approach as the customer's SDK capabilities path).
/// </summary>
public abstract class BaseTest
{
    protected const int DefaultTimeoutSeconds = 10;

    protected IOSDriver Driver { get; private set; } = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new AppiumOptions
        {
            PlatformName = "iOS",
            AutomationName = "XCUITest"
        };

        // The SDK redirects this local hub URL to BrowserStack
        Driver = new IOSDriver(new Uri("http://127.0.0.1:4723"), options);
    }

    [TearDown]
    public void TearDown()
    {
        if (Driver == null) return;

        try
        {
            var passed = TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed;
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                action = "setSessionStatus",
                arguments = new
                {
                    status = passed ? "passed" : "failed",
                    reason = $"Test '{TestContext.CurrentContext.Test.Name}' {(passed ? "passed" : "failed")}"
                }
            });
            Driver.ExecuteScript($"browserstack_executor: {payload}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not set BrowserStack session status: {ex.Message}");
        }
        finally
        {
            Driver.Quit();
        }
    }

    /// <summary>Explicit wait (10s default) for an element by accessibility id.</summary>
    protected IWebElement WaitFor(string accessibilityId, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds))
        {
            Message = $"Element with accessibility id '{accessibilityId}' was not found within {timeoutSeconds}s"
        };
        return wait.Until(d => d.FindElement(MobileBy.AccessibilityId(accessibilityId)));
    }
}
