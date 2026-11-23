using FrontSystems.MobileAutomation.iOS.Config;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Support.UI;

namespace FrontSystems.MobileAutomation.iOS.Helpers;

public static class LoginHelper
{
    public static void PerformLogin(AppiumDriver driver)
    {
        Console.WriteLine("Performing login via LoginHelper...");

        // Get credentials from environment or config
        var (username, password) = TestCredentialsReader.GetSecondaryCredentials();
        Console.WriteLine($"Using credentials: {username}");

        // Wait for app to fully load - increased from 2s to 5s
        Console.WriteLine("Waiting for app to fully load (5 seconds)...");
        Thread.Sleep(5000);

        // Verify app has actually loaded by checking for ANY elements
        WaitForAppToLoad(driver);

        // Handle onboarding welcome screen: click "Install/Next" to reveal login form.
        // AccessibilityId is masked by BrowserStack (***) so we locate by text of the inner Label.
        // ForwardButton is a ContentView (not a native Button), its Label renders as android.widget.TextView.
        // Check if already logged in before attempting onboarding flow.
        try
        {
            var posElement = driver.FindElement(By.Name("MenuStartSale"));
            if (posElement != null && posElement.Displayed)
            {
                Console.WriteLine("✓ App is already logged in (pre-onboarding check) - skipping login");
                return;
            }
        }
        catch (Exception) { }

        Console.WriteLine("Waiting for onboarding install/next button...");
        var isAndroid = DriverFactory.IsAndroidDriver(driver);
        // Four-locator strategy for iOS (shared ~30s budget), two for Android:
        //   1. BtnNextLoggInn (AutomationId on the ForwardButton ContentView wrapper) — 10s (both)
        //   2. iOS: btnScannerStepNext (AutomationId on the inner Label, newer iOS may hide the ContentView) — 10s
        //      Android: text XPath fallback — 20s
        //   3. iOS: text-based XPath — 5s
        //   4. iOS: ByIosNSPredicate (NSPredicate on native XCUITest attributes, catches iOS 26
        //      where MAUI does not propagate AutomationId to accessibilityIdentifier) — 5s
        var installBtn = WaitForElement(driver, MobileBy.AccessibilityId("BtnNextLoggInn"), 10);
        if (installBtn == null)
            installBtn = isAndroid
                ? WaitForElement(driver, By.XPath("//android.widget.TextView[contains(@text, 'Install') or contains(@text, 'Installer')]"), 20)
                : WaitForElement(driver, MobileBy.AccessibilityId("btnScannerStepNext"), 10);
        if (installBtn == null && !isAndroid)
            installBtn = WaitForElement(driver, By.XPath("//*[contains(@name, 'Install') or contains(@label, 'Install') or contains(@value, 'Install')]"), 5);
        // iOS 26: MAUI may not propagate AutomationId to XCUITest accessibilityIdentifier.
        // Use NSPredicate to query native XCUITest attributes directly.
        if (installBtn == null && !isAndroid)
            installBtn = WaitForElement(driver, new ByIosNSPredicate("label CONTAINS 'Install' OR value CONTAINS 'Install' OR label CONTAINS 'Installer'"), 5);
        if (installBtn != null)
        {
            Console.WriteLine($"Found install button (text: '{installBtn.Text}'), clicking it");
            installBtn.Click();
            Thread.Sleep(1000);
        }
        else
        {
            // Install button not found — check if login screen or POS screen is already visible (persisted app state).
            var loginFieldsVisible = driver.FindElements(By.XPath(
                "//XCUIElementTypeTextField | //XCUIElementTypeSecureTextField | //android.widget.EditText")).Count > 0;
            if (loginFieldsVisible)
            {
                Console.WriteLine("Install button not found but login screen already visible — skipping onboarding");
            }
            else
            {
                Console.WriteLine("Onboarding install button not found and login screen not visible — dumping diagnostics");
                TakeDebugScreenshot(driver, "no_install_button");
                DumpAllInteractiveElements(driver);
                try
                {
                    var pageSource = driver.PageSource;
                    Console.WriteLine("=== PageSource (first 3000 chars) ===");
                    Console.WriteLine(pageSource.Substring(0, Math.Min(3000, pageSource.Length)));
                    Console.WriteLine("=== End PageSource ===");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get PageSource: {ex.Message}");
                }
                throw new Exception("Onboarding install button not found and login screen not visible — cannot proceed");
            }
        }

        // Try to find and click scanner button if present
        var nextBtn = WaitForElement(driver, MobileBy.AccessibilityId("btnScannerStepNext"), 1);
        if (nextBtn != null)
        {
            Console.WriteLine("Found scanner next button, clicking it");
            nextBtn.Click();
            Thread.Sleep(500);
        }

        // Wait specifically for login screen (text input fields) to appear
        Console.WriteLine("Waiting for login screen to appear...");
        WaitForLoginScreenToAppear(driver);

        // Enter username - try multiple locator strategies
        Console.WriteLine($"Entering username: {username}");
        var usernameField = FindUsernameField(driver);
        if (usernameField == null)
        {
            Console.WriteLine("Username field not found - printing available elements for debugging");
            PrintAvailableElements(driver);
            TakeDebugScreenshot(driver, "login_failed_no_username_field");
            throw new Exception("Username field not found after trying multiple locators");
        }
        usernameField.Clear();
        usernameField.SendKeys(username);

        DismissKeyboard(driver);

        // Enter password
        Console.WriteLine("Entering password...");
        var passwordField = WaitForSecureTextField(driver, 10);
        if (passwordField == null)
        {
            Console.WriteLine("Password field not found");
            PrintAvailableElements(driver);
            TakeDebugScreenshot(driver, "login_failed_no_password_field");
            throw new Exception("Password field not found");
        }
        passwordField.Click();
        passwordField.Clear();
        passwordField.SendKeys(password);

        DismissKeyboard(driver);

        // Click login button.
        // AccessibilityId is masked by BrowserStack, so locate by text.
        // btnLoggInn is a native MAUI Button - renders as android.widget.Button with @text set.
        Console.WriteLine("Clicking login button...");
        var loginBtn = WaitForElement(driver,
            By.XPath("//android.widget.Button[contains(@text, 'Log in') or contains(@text, 'Logg')]"), 5);
        if (loginBtn == null)
            loginBtn = WaitForElement(driver,
                By.XPath("//XCUIElementTypeButton[contains(@name, 'Log in') or contains(@name, 'Logg') or @name='btnLoggInn']"), 5);
        if (loginBtn != null)
        {
            Thread.Sleep(500);
            try
            {
                loginBtn.Click();
                Console.WriteLine("Login button clicked");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to click login button: {ex.Message}");
                throw;
            }
        }
        else
        {
            Console.WriteLine("Login button not found - may have auto-logged in or different flow");
        }

        // Wait for login to complete
        Console.WriteLine("Waiting for login to complete...");
        Thread.Sleep(3000); // Give time for any permission popups

        Console.WriteLine("Login completed successfully via LoginHelper");
    }

    private static void WaitForAppToLoad(AppiumDriver driver)
    {
        Console.WriteLine("Checking if app has loaded...");
        bool pageSourceLogged = false;
        bool reactivated = false;
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
            {
                try
                {
                    var elements = d.FindElements(By.XPath("//*"));

                    if (elements.Count == 0 && !pageSourceLogged)
                    {
                        try
                        {
                            Console.WriteLine("  === PageSource (first 1000 chars) ===");
                            var pageSource = d.PageSource;
                            Console.WriteLine(pageSource.Substring(0, Math.Min(1000, pageSource.Length)));
                            Console.WriteLine("  === End PageSource ===");
                            pageSourceLogged = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Failed to get PageSource: {ex.Message}");
                        }
                    }

                    // FRO-16728 follow-up: `elements.Count > 5` alone is a false positive when the
                    // driver is still attached to the home screen — SpringBoard's own invisible
                    // XCUITestElementTypeOther wrapper chrome easily clears 5 elements. Reject any
                    // tree that isn't actually the app under test, and re-activate it once.
                    // Cast d rather than closing over the outer `driver` — WebDriverWait always
                    // invokes this predicate with the same instance it was constructed with, but
                    // staying on d keeps the predicate self-contained if that ever changes (e.g.
                    // a wrapped/proxied IWebDriver). Use a safe `as` cast so a wrapped/proxied
                    // driver falls back to the plain element-count check instead of throwing.
                    // IsWrongApp only detects the iOS SpringBoard bundle id, so it's skipped
                    // entirely on Android (and PageSource, which it reads, is relatively
                    // expensive/flaky there).
                    var appiumDriver = d as AppiumDriver;
                    var checkForWrongApp = appiumDriver != null && !DriverFactory.IsAndroidDriver(appiumDriver);
                    if (checkForWrongApp && elements.Count > 5 && IsWrongApp(appiumDriver!))
                    {
                        if (!reactivated)
                        {
                            Console.WriteLine("  ⚠ Accessibility tree belongs to the home screen, not the app — re-activating.");
                            var appId = DriverFactory.AppBundleId;
                            try { appiumDriver!.ActivateApp(appId); } catch (Exception ex) { Console.WriteLine($"  Re-activate failed: {ex.Message}"); }
                            reactivated = true;
                        }
                        return false;
                    }

                    return elements.Count > 5;
                }
                catch { return false; }
            });
            Console.WriteLine("✓ App has loaded");
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("⚠ App load check timed out, continuing anyway...");
            try
            {
                Console.WriteLine("=== Final PageSource (first 2000 chars) ===");
                var pageSource = driver.PageSource;
                Console.WriteLine(pageSource.Substring(0, Math.Min(2000, pageSource.Length)));
                Console.WriteLine("=== End Final PageSource ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get final PageSource: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// True when the driver's accessibility root is iOS SpringBoard rather than the app under
    /// test — happens right after a fresh BrowserStack session starts. iOS-only: this checks
    /// for the SpringBoard bundle id specifically, there is no Android launcher detection here.
    /// Also true when PageSource itself throws: a session that can't even report its own tree
    /// isn't verified as "the app", so the caller should keep polling rather than treat it as
    /// ready by falling through to the plain element-count check.
    /// </summary>
    private static bool IsWrongApp(AppiumDriver driver)
    {
        try
        {
            var pageSource = driver.PageSource;
            return pageSource.Contains("com.apple.springboard", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static void WaitForLoginScreenToAppear(AppiumDriver driver)
    {
        Console.WriteLine("Waiting for login screen (text input fields) to appear...");
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            wait.Until(d =>
            {
                try
                {
                    // Check for any text input elements
                    var textFields = d.FindElements(By.XPath("//XCUIElementTypeTextField | //XCUIElementTypeSecureTextField | //android.widget.EditText"));
                    return textFields.Count > 0;
                }
                catch { return false; }
            });
            Console.WriteLine("✓ Login screen appeared");
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("✗ Login screen never appeared - will attempt to find field anyway");
        }
    }

    private static AppiumElement? FindUsernameField(AppiumDriver driver)
    {
        // Try multiple locator strategies - both stable IDs and name-based fallbacks
        By[] locators =
        [
                // Primary: AccessibilityId (maps to content-desc on Android, accessibility identifier on iOS)
                MobileBy.AccessibilityId("AutUserName"),

                // iOS XPath
                By.XPath("//XCUIElementTypeTextField[@identifier='AutUserName']"),
                By.XPath("//XCUIElementTypeTextField[@identifier='AutUserName' or @name='AutUserName']"),
                By.XPath("//XCUIElementTypeTextField[contains(@name, 'User')]"),
                By.XPath("//XCUIElementTypeTextField[contains(@placeholder, 'user') or contains(@placeholder, 'email')]"),
                By.XPath("//XCUIElementTypeTextField[contains(@placeholder, 'User') or contains(@placeholder, 'Email')]"),

                // Android XPath (resource-id and hint)
                By.XPath("//android.widget.EditText[@resource-id='com.eg.dk.frontsystems:id/AutUserName']"),
                By.XPath("//android.widget.EditText[contains(@hint, 'user') or contains(@hint, 'email')]"),
                By.XPath("//android.widget.EditText[contains(@hint, 'User') or contains(@hint, 'Email')]"),

                // Fallback: Position-based (last resort)
                By.XPath("//XCUIElementTypeTextField[1]"),
                By.XPath("//android.widget.EditText[1]")
        ];

        foreach (var locator in locators)
        {
            var field = WaitForElement(driver, locator, 1);
            if (field != null)
            {
                Console.WriteLine($"✓ Found username field with locator: {locator}");
                return field;
            }
        }

        return null;
    }

    private static void DumpAllInteractiveElements(AppiumDriver driver)
    {
        try
        {
            Console.WriteLine("\n=== ALL BUTTONS ===");
            var buttons = driver.FindElements(By.XPath("//android.widget.Button | //android.widget.ImageButton | //XCUIElementTypeButton"));
            Console.WriteLine($"Buttons found: {buttons.Count}");
            for (int i = 0; i < buttons.Count; i++)
            {
                try
                {
                    Console.WriteLine($"  Button {i + 1}:");
                    Console.WriteLine($"    class:        '{buttons[i].TagName}'");
                    Console.WriteLine($"    content-desc: '{buttons[i].GetAttribute("content-desc")}'");
                    Console.WriteLine($"    resource-id:  '{buttons[i].GetAttribute("resource-id")}'");
                    Console.WriteLine($"    text:         '{buttons[i].GetAttribute("text")}'");
                    Console.WriteLine($"    name:         '{buttons[i].GetAttribute("name")}'");
                    Console.WriteLine($"    label:        '{buttons[i].GetAttribute("label")}'");
                    Console.WriteLine($"    clickable:    '{buttons[i].GetAttribute("clickable")}'");
                    Console.WriteLine($"    displayed:    '{buttons[i].GetAttribute("displayed")}'");
                    Console.WriteLine($"    bounds:       '{buttons[i].GetAttribute("bounds")}'");
                }
                catch { }
            }

            Console.WriteLine("\n=== ALL CLICKABLE ELEMENTS ===");
            var clickables = driver.FindElements(By.XPath("//*[@clickable='true']"));
            Console.WriteLine($"Clickable elements found: {clickables.Count}");
            for (int i = 0; i < Math.Min(clickables.Count, 20); i++)
            {
                try
                {
                    Console.WriteLine($"  Clickable {i + 1}:");
                    Console.WriteLine($"    class:        '{clickables[i].TagName}'");
                    Console.WriteLine($"    content-desc: '{clickables[i].GetAttribute("content-desc")}'");
                    Console.WriteLine($"    resource-id:  '{clickables[i].GetAttribute("resource-id")}'");
                    Console.WriteLine($"    text:         '{clickables[i].GetAttribute("text")}'");
                    Console.WriteLine($"    bounds:       '{clickables[i].GetAttribute("bounds")}'");
                }
                catch { }
            }
            Console.WriteLine("=== END INTERACTIVE ELEMENTS ===\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error dumping interactive elements: {ex.Message}");
        }
    }

    private static void PrintAvailableElements(AppiumDriver driver)
    {
        try
        {
            Console.WriteLine("\n=== AVAILABLE TEXT INPUT ELEMENTS ===");
            var textFields = driver.FindElements(By.XPath("//XCUIElementTypeTextField | //XCUIElementTypeSecureTextField | //android.widget.EditText"));

            Console.WriteLine($"Text fields found: {textFields.Count}");
            for (int i = 0; i < Math.Min(textFields.Count, 10); i++)
            {
                try
                {
                    var name = textFields[i].GetAttribute("name") ?? "";
                    var resourceId = textFields[i].GetAttribute("resource-id") ?? "";
                    var identifier = textFields[i].GetAttribute("identifier") ?? "";
                    var placeholder = textFields[i].GetAttribute("placeholder") ?? "";
                    var hint = textFields[i].GetAttribute("hint") ?? "";
                    var type = textFields[i].TagName;
                    Console.WriteLine($"  {i + 1}. Type: {type}");
                    Console.WriteLine($"      Name: '{name}', ID: '{resourceId}', Identifier: '{identifier}'");
                    Console.WriteLine($"      Placeholder: '{placeholder}', Hint: '{hint}'");
                }
                catch { }
            }
            Console.WriteLine("=== END ELEMENTS ===\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error printing elements: {ex.Message}");
        }
    }

    private static void TakeDebugScreenshot(AppiumDriver driver, string name)
    {
        try
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = $"Debug_{name}_{timestamp}.png";
            screenshot.SaveAsFile(path);
            Console.WriteLine($"Screenshot saved: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to take screenshot: {ex.Message}");
        }
    }

    private static AppiumElement? WaitForElement(AppiumDriver driver, By locator, int timeoutSeconds)
    {
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(driver, TimeSpan.Zero);
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(_ =>
            {
                try
                {
                    var element = driver.FindElement(locator);
                    try
                    {
                        return element != null && element.Displayed ? element : null;
                    }
                    catch (StaleElementReferenceException)
                    {
                        return null;
                    }
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
                catch (InvalidSelectorException)
                {
                    return null;
                }
            });
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
        finally
        {
            DriverFactory.SetImplicitWait(driver, originalImplicitWait);
        }
    }

    private static AppiumElement? WaitForSecureTextField(AppiumDriver driver, int timeoutSeconds)
    {
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(driver, TimeSpan.Zero);
        try
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(_ =>
            {
                try
                {
                    var secureFields = driver.FindElements(By.XPath(
                        "//XCUIElementTypeSecureTextField | //android.widget.EditText[@password='true']"));
                    foreach (var field in secureFields)
                    {
                        if (field.Displayed && field.Enabled)
                            return field;
                    }

                    // Fallback: second EditText in the form (username is first, password is second)
                    var allEditTexts = driver.FindElements(By.XPath("//android.widget.EditText"));
                    if (allEditTexts.Count > 1)
                    {
                        var candidate = allEditTexts[1];
                        if (candidate.Displayed && candidate.Enabled)
                            return candidate;
                    }

                    return null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            });
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
        finally
        {
            DriverFactory.SetImplicitWait(driver, originalImplicitWait);
        }
    }

    private static void TryHideKeyboardAndroid(AppiumDriver driver)
    {
        try
        {
            driver.ExecuteScript("mobile: performEditorAction", new Dictionary<string, object> { { "action", "done" } });
            Thread.Sleep(300);
        }
        catch (Exception) { }
    }

    private static void DismissKeyboard(AppiumDriver driver)
    {
        // On Android, dismiss keyboard via mobile:performEditorAction with "done".
        if (DriverFactory.IsAndroidDriver(driver))
        {
            TryHideKeyboardAndroid(driver);
            return;
        }

        try
        {
            var returnKey = driver.FindElement(By.Name("return"));
            if (returnKey.Displayed)
            {
                returnKey.Click();
                Thread.Sleep(500);
                return;
            }
        }
        catch (Exception) { }

        string[] returnKeys = ["Return", "Done", "done", "Search"];
        foreach (var keyName in returnKeys)
        {
            try
            {
                var key = driver.FindElement(By.Name(keyName));
                if (key.Displayed)
                {
                    key.Click();
                    Thread.Sleep(500);
                    return;
                }
            }
            catch (Exception) { }
        }
    }
}
