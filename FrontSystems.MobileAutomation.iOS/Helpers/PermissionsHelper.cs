using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Helpers;

public static class PermissionsHelper
{
    public static void HandlePermissions(AppiumDriver driver, int timeoutSeconds = 15)
    {
        Console.WriteLine("🔍 PermissionsHelper.HandlePermissions called with timeout: " + timeoutSeconds + " seconds");

        if (DriverFactory.IsAndroidDriver(driver))
            HandleAndroidPermissions(driver, timeoutSeconds);
        else
            HandleIOSPermissions(driver, timeoutSeconds);
    }

    private static void HandleIOSPermissions(AppiumDriver driver, int timeoutSeconds)
    {
        int attempts = 0;
        int maxAttempts = 15;
        DateTime startTime = DateTime.Now;
        int checkCount = 0;

        while (attempts < maxAttempts && (DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            checkCount++;
            try
            {
                var alerts = driver.FindElements(By.XPath("//XCUIElementTypeAlert"));

                if (checkCount % 10 == 0)
                    Console.WriteLine($"  Check #{checkCount}: Found {alerts.Count} alert(s)");

                if (alerts.Count > 0)
                {
                    Console.WriteLine($"✅ Found permission dialog #{attempts + 1}");
                    var alert = alerts[0];

                    if (ClickIOSAllowButton(alert))
                    {
                        Console.WriteLine("✅ Clicked Allow button");
                        attempts++;
                        Thread.Sleep(300);
                        continue;
                    }

                    if (ClickIOSOKButton(alert))
                    {
                        Console.WriteLine("✅ Clicked OK button");
                        attempts++;
                        Thread.Sleep(300);
                        continue;
                    }

                    Console.WriteLine("⚠️ Permission dialog found but no Allow/OK button found");
                    break;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                if (checkCount % 10 == 0)
                    Console.WriteLine($"  Exception on check #{checkCount}: {ex.Message}");
                Thread.Sleep(100);
            }
        }

        Console.WriteLine($"PermissionsHelper finished. Found and handled {attempts} dialog(s). Performed {checkCount} checks in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }

    private static void HandleAndroidPermissions(AppiumDriver driver, int timeoutSeconds)
    {
        int attempts = 0;
        DateTime startTime = DateTime.Now;

        while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
        {
            try
            {
                var allowButtons = driver.FindElements(By.XPath(
                    "//android.widget.Button[translate(@text,'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ')='ALLOW' or @text='Tillat' or @text='OK' or @text='Ok']"));

                if (allowButtons.Count > 0 && allowButtons[0].Displayed)
                {
                    Console.WriteLine("✅ Found Android permission dialog, clicking Allow...");
                    allowButtons[0].Click();
                    attempts++;
                    Thread.Sleep(500);
                    continue;
                }
            }
            catch (Exception) { }

            Thread.Sleep(500);
        }

        Console.WriteLine($"✅ Android permissions handling done. Handled {attempts} dialog(s) in {(DateTime.Now - startTime).TotalSeconds:F1} seconds");
    }

    private static bool ClickIOSAllowButton(AppiumElement alert)
    {
        try
        {
            var allowButtons = alert.FindElements(By.XPath(".//XCUIElementTypeButton[contains(@name, 'Allow') or contains(@label, 'Allow')]"));

            if (allowButtons.Count > 0)
            {
                Console.WriteLine("  Found Allow button, clicking...");
                allowButtons[0].Click();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error clicking Allow: {ex.Message}");
        }

        return false;
    }

    private static bool ClickIOSOKButton(AppiumElement alert)
    {
        try
        {
            var okButtons = alert.FindElements(By.XPath(".//XCUIElementTypeButton[contains(@name, 'OK') or contains(@label, 'OK')]"));

            if (okButtons.Count > 0)
            {
                Console.WriteLine("  Found OK button, clicking...");
                okButtons[0].Click();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error clicking OK: {ex.Message}");
        }

        return false;
    }
}
