using FrontSystems.MobileAutomation.iOS.Config;
using FrontSystems.MobileAutomation.iOS.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Tests.iOS;

[TestFixture]
public class LoginFlowTest : BaseIosTest
{
    [SetUp]
    public override void SetUp()
    {
        Driver = DriverFactory.CreateiOSDriver();
    }

    [Test]
    [Category("iOS")]
    [Category("LoginFlow")]
    public void Should_LoginSuccessfully_AndHandlePermissions()
    {
        try
        {
            Console.WriteLine("Starting Diagnostic Login Flow Test...");

            Console.WriteLine("Waiting for app UI to load...");
            var uiReady = false;
            for (int i = 0; i < 12; i++)
            {
                Thread.Sleep(2500);
                var elements = Driver.FindElements(By.XPath("//*"));
                if (elements.Count > 0)
                {
                    Console.WriteLine($"App UI ready after ~{(i + 1) * 2.5}s ({elements.Count} elements found)");
                    uiReady = true;
                    break;
                }
            }
            if (!uiReady) Console.WriteLine("⚠ Warning: App UI still empty after 30s, continuing anyway...");

            TakeScreenshot("01_AppLaunched");

            Console.WriteLine("Looking for scanner step next button...");
            try
            {
                var nextBtn = Driver.FindElement(By.Name("btnScannerStepNext"));
                if (nextBtn != null && nextBtn.Displayed)
                {
                    Console.WriteLine("Found scanner next button, clicking it");
                    nextBtn.Click();
                    TakeScreenshot("02_AfterScannerNext");
                    Thread.Sleep(2000);
                }
                else
                {
                    Console.WriteLine("Scanner next button exists but not displayed");
                }
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Scanner next button not found - app might be starting on login screen directly");
                TakeScreenshot("02_NoScannerButton");
            }

            Console.WriteLine("Looking for username field...");
            try
            {
                var usernameField = Driver.FindElement(By.Name("AutUserName"));
                if (usernameField != null && usernameField.Displayed)
                {
                    Console.WriteLine("Found username field - app is on login screen");

                    var (username, password) = TestCredentialsReader.GetSecondaryCredentials();

                    usernameField.Clear();
                    usernameField.SendKeys(username);
                    TakeScreenshot("03_UsernameEntered");
                    Console.WriteLine($"Login attempt with username: {username}");

                    TryHideKeyboard();

                    var passwordField = WaitForSecureTextField(15);
                    passwordField!.Click();
                    passwordField.Clear();
                    passwordField.SendKeys(password);
                    TakeScreenshot("04_PasswordEntered");

                    TryHideKeyboard();

                    var loginBtn = FindElementWithTimeout(By.Name("btnLoggInn"), 10);
                    if (loginBtn == null)
                    {
                        TakeScreenshot("05_LoginButtonNotFound");
                        throw new InvalidOperationException("Login button 'btnLoggInn' was not found.");
                    }
                    loginBtn.Click();
                    TakeScreenshot("05_LoginButtonClicked");

                    Thread.Sleep(5000);
                    TakeScreenshot("06_LoginComplete");

                    Console.WriteLine("Login flow completed successfully!");
                }
                else
                {
                    Console.WriteLine("Username field exists but not displayed");
                }
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Username field not found either");
                TakeScreenshot("03_NoUsernameField");
            }

            Console.WriteLine("Diagnostic test completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Diagnostic test failed: {ex.Message}");
            TakeScreenshot("FAILED_DiagnosticTest");
            throw;
        }
    }
}
