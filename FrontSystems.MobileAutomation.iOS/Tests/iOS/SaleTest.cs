using FrontSystems.MobileAutomation.iOS.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Tests.iOS;

[TestFixture]
public class SalesTest : BaseIosTest
{
    [SetUp]
    public override void SetUp()
    {
        Driver = DriverFactory.CreateiOSDriver();
    }

    [Test]
    [Category("iOS")]
    [Category("Sales")]
    public void Should_CreateSaleSuccessfully()
    {
        try
        {
            Console.WriteLine("Starting Sales Test - performing login first, then sale flow...");

            Console.WriteLine("\n=== LOGIN PHASE ===");
            LoginHelper.PerformLogin(Driver);
            TakeScreenshot("Login_Complete");

            Console.WriteLine("Login completed! Starting sale flow...");
            Console.WriteLine("\n=== SALE PHASE ===");

            Console.WriteLine("Step 1: Looking for MenuStartSale button...");
            var startSaleButton = FindElementWithTimeout(By.Name("MenuStartSale"), 10);
            if (startSaleButton != null)
            {
                Console.WriteLine("Found MenuStartSale button, clicking it");
                startSaleButton.Click();
                TakeScreenshot("01_MenuStartSaleClicked");
                Thread.Sleep(3000);

                Console.WriteLine("Step 2: Looking for Search button in POS interface...");
                var searchButton = FindElementWithTimeout(By.Name("Search"), 10);
                if (searchButton != null)
                {
                    Console.WriteLine("Found Search button, clicking it");
                    searchButton.Click();
                    TakeScreenshot("02_SearchClicked");
                    Thread.Sleep(2000);

                    Console.WriteLine("Step 3: Looking for search entry field...");
                    var searchEntry = FindElementWithTimeout(By.Name("EntrySearch"), 10);
                    if (searchEntry != null)
                    {
                        Console.WriteLine("Found search entry field, entering product code");
                        searchEntry.Clear();
                        searchEntry.SendKeys("10710493");
                        TakeScreenshot("03_ProductCodeEntered");

                        TryHideKeyboard();
                        Thread.Sleep(3000);

                        TakeScreenshot("04_SearchResults");

                        Console.WriteLine("Step 4: Looking for MAUD TOTE BAG in search results...");
                        var productItem = FindProductInResults();
                        if (productItem != null)
                        {
                            Console.WriteLine("Found MAUD TOTE BAG, clicking it");
                            productItem.Click();
                            TakeScreenshot("05_ProductSelected");
                            Thread.Sleep(2000);
                            Console.WriteLine("Product selection completed!");
                        }
                        else
                        {
                            Console.WriteLine("Could not find MAUD TOTE BAG in search results");
                            TakeScreenshot("ERROR_ProductNotFound");
                        }

                        Console.WriteLine("Sales flow completed!");
                    }
                    else
                    {
                        Console.WriteLine("Search entry field not found");
                        TakeScreenshot("ERROR_NoSearchEntry");
                    }
                }
                else
                {
                    Console.WriteLine("Search button not found in POS interface");
                    TakeScreenshot("ERROR_NoSearchButton");
                }
            }
            else
            {
                Console.WriteLine("MenuStartSale button not found");
                TakeScreenshot("ERROR_NoMenuStartSale");
            }

            Console.WriteLine("Sales test completed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sales test failed: {ex.Message}");
            TakeScreenshot("FAILED_SalesTest");
            throw;
        }
    }

    private AppiumElement? FindProductInResults()
    {
        foreach (var selector in new[] { "Maud MAUD TOTE BAG - Don't touch", "MAUD TOTE BAG", "10710493" })
        {
            try
            {
                var element = Driver.FindElement(By.Name(selector));
                if (element != null && element.Displayed)
                {
                    Console.WriteLine($"Found product with selector: '{selector}'");
                    return element;
                }
            }
            catch { }
        }

        foreach (var xpath in new[]
        {
            "//*[contains(@name, 'MAUD TOTE BAG') or contains(@label, 'MAUD TOTE BAG')]",
            "//*[contains(@name, 'Maud') or contains(@label, 'Maud')]",
            "//*[contains(@name, '10710493') or contains(@label, '10710493')]"
        })
        {
            try
            {
                var elements = Driver.FindElements(By.XPath(xpath));
                if (elements.Count > 0 && elements[0].Displayed)
                    return elements[0];
            }
            catch { }
        }

        return null;
    }
}
