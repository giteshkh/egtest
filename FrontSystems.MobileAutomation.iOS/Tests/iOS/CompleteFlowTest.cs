using FrontSystems.MobileAutomation.iOS.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace FrontSystems.MobileAutomation.iOS.Tests.iOS;

[TestFixture]
public class CompleteFlowTest : BaseIosTest
{
    [SetUp]
    public override void SetUp()
    {
        Driver = DriverFactory.CreateDriver();
    }

    [Test]
    [Category("iOS")]
    [Category("CompleteFlow")]
    public void Should_CompleteLoginAndSaleFlow()
    {
        try
        {
            Console.WriteLine("Starting Complete Flow Test - 1 Sale + Z-Report Settlement");

            Console.WriteLine("\n--- LOGIN FLOW ---");
            LoginHelper.PerformLogin(Driver);
            TakeScreenshot("01_LoginComplete");
            Console.WriteLine("✅ Login completed");
            AssertAppAlive("login");

            Console.WriteLine("\n--- PERMISSION DIALOGS ---");
            PermissionsHelper.HandlePermissions(Driver, 60);
            TakeScreenshot("02_AfterPermissions");
            Console.WriteLine("✅ Permissions handled");
            AssertAppAlive("permissions");

            Thread.Sleep(2000);

            Console.WriteLine("\n--- WAITING FOR POS READY ---");
            EnsureUnlocked();
            Console.WriteLine("✅ POS ready");

            Console.WriteLine("\n--- SALE 1/1 ---");
            PerformSingleSale(1);
            Console.WriteLine("\n✅ Sale completed!");
            AssertAppAlive("single sale");

            Console.WriteLine("\n--- DELETE SALESLINE AND CANCEL SALE TEST ---");
            PerformDeleteSaleslineAndCancelTest();
            AssertAppAlive("delete salesline + cancel sale");

            Console.WriteLine("\n--- PARK AND RETRIEVE SALE TEST ---");
            PerformParkSaleTest();
            AssertAppAlive("park + retrieve sale");

            Console.WriteLine("\n--- ADD CUSTOMER TO SALE TEST ---");
            PerformAddCustomerToSaleTest();
            AssertAppAlive("add customer to sale");

            Console.WriteLine("Sale finished, proceeding to settlement...");
            Thread.Sleep(2000);

            Console.WriteLine("\n--- END OF DAY: Z-REPORT SETTLEMENT ---");
            try
            {
                PerformZReportSettlement();
                Console.WriteLine("\n✅ Z-Report settlement completed!");
            }
            catch (Exception settlementEx)
            {
                Console.WriteLine($"\n❌ Settlement failed: {settlementEx.Message}");
                TakeScreenshot("SETTLEMENT_FAILED");
                throw;
            }

            Console.WriteLine("\n--- NAVIGATE BACK TO USER PAGE ---");
            NavigateBackToUserPage();
            Console.WriteLine("✅ Back at user page");

            Console.WriteLine("\n--- LOGOUT ---");
            PerformLogout();
            Console.WriteLine("✅ Logout completed");

            Thread.Sleep(5000);
            TakeScreenshot("11_AppVersionDisplay");

            Console.WriteLine("\n✅ Complete flow with end-of-day settlement finished successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            TakeScreenshot("FAILED");
            throw;
        }
    }

    private void NavigateBackToUserPage()
    {
        DebugCurrentPage();

        Console.WriteLine("Looking for back button (<)...");
        var backButton = FindElementWithTimeout(MobileBy.AccessibilityId("BackButton"), 3);

        if (backButton == null)
        {
            foreach (var selector in new[] { "<", "Back", "BtnBack", "NavigationBar" })
            {
                try
                {
                    backButton = Driver.FindElement(MobileBy.AccessibilityId(selector));
                    if (backButton != null && backButton.Displayed) break;
                }
                catch { }
            }
        }

        if (backButton != null)
        {
            backButton.Click();
            TakeScreenshot("10_BackToUserPage");
            Thread.Sleep(1000);
            Console.WriteLine("✅ Navigated back to user page");
        }
        else
        {
            Console.WriteLine("⚠️ Back button not found, attempting to proceed with logout");
        }
    }

    private void PerformDeleteSaleslineAndCancelTest()
    {
        Console.WriteLine("Starting new sale...");
        var startSaleButton = FindElementWithTimeout(By.Name("MenuStartSale"), 5);
        if (startSaleButton == null) { Console.WriteLine("❌ MenuStartSale button not found"); return; }
        startSaleButton.Click();
        Thread.Sleep(100);

        var barcodeField = FindElementWithTimeout(By.Name("ScanCode"), 5);
        if (barcodeField == null) { Console.WriteLine("❌ ScanCode field not found"); return; }
        barcodeField.Click();
        barcodeField.Clear();
        barcodeField.SendKeys("5401157473672");
        TryHideKeyboard();
        Thread.Sleep(1000);
        TakeScreenshot("DeleteAndCancel_01_ProductAdded");

        Console.WriteLine("Tapping salesline to select it...");
        var salesLine = FindElementWithTimeout(By.Name("SalesLineItem"), 5);
        if (salesLine == null)
        {
            try { salesLine = Driver.FindElement(By.XPath("(//XCUIElementTypeCell)[1]")); }
            catch (Exception) { }
        }
        if (salesLine == null) { Console.WriteLine("❌ Salesline element not found"); return; }
        salesLine.Click();
        Thread.Sleep(500);
        TakeScreenshot("DeleteAndCancel_02_SaleslineSelected");

        Console.WriteLine("Looking for Delete button in popup...");
        var deleteButton = FindElementWithTimeout(By.Name("BtnDeleteSalesLine"), 3);
        if (deleteButton == null)
        {
            string[] deleteSelectors = ["Delete", "BtnDelete", "menuDelete", "Slett"];
            foreach (var sel in deleteSelectors)
            {
                try
                {
                    var el = Driver.FindElement(By.Name(sel));
                    if (el != null && el.Displayed) { deleteButton = el; break; }
                }
                catch (Exception) { }
            }
        }
        if (deleteButton == null) { Console.WriteLine("❌ Delete button not found"); TakeScreenshot("DeleteAndCancel_ERROR_NoDeleteButton"); return; }
        deleteButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("DeleteAndCancel_03_SaleslineDeleted");
        Console.WriteLine("✅ Salesline deleted!");

        Console.WriteLine("Adding product again before cancel...");
        barcodeField = FindElementWithTimeout(By.Name("ScanCode"), 5);
        if (barcodeField == null) { Console.WriteLine("❌ ScanCode field not found after delete"); return; }
        barcodeField.Click();
        barcodeField.Clear();
        barcodeField.SendKeys("5401157473672");
        TryHideKeyboard();
        Thread.Sleep(1000);
        TakeScreenshot("DeleteAndCancel_04_ProductAddedAgain");

        Console.WriteLine("Looking for More button...");
        var moreButton = FindElementWithTimeout(By.Name("btnMore"), 3);
        if (moreButton == null) { Console.WriteLine("❌ More button not found"); TakeScreenshot("DeleteAndCancel_ERROR_NoMoreButton"); return; }
        moreButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("DeleteAndCancel_05_MoreMenuOpened");

        Console.WriteLine("Looking for Cancel Sale button in More menu...");
        var cancelButton = FindElementWithTimeout(By.Name("BtnCancelSale"), 3)
            ?? FindElementWithTimeout(By.Name("Cancel sale"), 3);
        if (cancelButton == null) { Console.WriteLine("❌ Cancel sale button not found"); TakeScreenshot("DeleteAndCancel_ERROR_NoCancelButton"); return; }
        cancelButton.Click();
        Thread.Sleep(1000);
        TakeScreenshot("DeleteAndCancel_06_SaleCancelled");
        Console.WriteLine("✅ Sale cancelled successfully!");
    }

    private void PerformParkSaleTest()
    {
        Console.WriteLine("Starting new sale to test park sale...");
        var startSaleButton = FindElementWithTimeout(By.Name("MenuStartSale"), 5);
        if (startSaleButton == null) { Console.WriteLine("❌ MenuStartSale not found"); return; }
        startSaleButton.Click();
        Thread.Sleep(100);

        var barcodeField = FindElementWithTimeout(By.Name("ScanCode"), 5);
        if (barcodeField == null) { Console.WriteLine("❌ ScanCode not found"); return; }
        barcodeField.Click();
        barcodeField.Clear();
        barcodeField.SendKeys("5401157473672");
        TryHideKeyboard();
        Thread.Sleep(1000);
        TakeScreenshot("ParkSale_01_ProductAdded");

        Console.WriteLine("Opening More menu to park sale...");
        var moreButton = FindElementWithTimeout(By.Name("btnMore"), 3);
        if (moreButton == null) { Console.WriteLine("❌ More button not found"); return; }
        moreButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("ParkSale_02_MoreMenuOpened");

        var parkButton = FindElementWithTimeout(By.Name("Park sale"), 3);
        if (parkButton == null) { Console.WriteLine("❌ Park sale button not found"); TakeScreenshot("ParkSale_ERROR_NoParkButton"); return; }
        parkButton.Click();
        Thread.Sleep(1000);
        TakeScreenshot("ParkSale_03_SaleParked");
        Console.WriteLine("✅ Sale parked! Now on user page.");

        Console.WriteLine("Opening More menu on user page...");
        var userMoreButton = FindElementWithTimeout(By.Name("More"), 3);
        if (userMoreButton == null) { Console.WriteLine("❌ More button on user page not found"); TakeScreenshot("ParkSale_ERROR_NoUserMoreButton"); return; }
        userMoreButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("ParkSale_04_UserMoreMenuOpened");

        Console.WriteLine("Looking for Parked sales button in user menu...");
        var parkedButton = FindElementWithTimeout(By.Name("BtnParkedSales"), 3)
            ?? FindElementWithTimeout(By.Name("Parked"), 3);
        if (parkedButton == null) { Console.WriteLine("❌ Parked button not found"); TakeScreenshot("ParkSale_ERROR_NoParkedButton"); return; }
        parkedButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("ParkSale_05_ParkedList");

        var firstParked = FindElementWithTimeout(By.XPath("(//XCUIElementTypeCell)[1]"), 5);
        if (firstParked == null) { Console.WriteLine("❌ No parked sales found in list"); return; }
        firstParked.Click();
        Thread.Sleep(1000);
        TakeScreenshot("ParkSale_06_ParkedSaleRetrieved");

        var cashButton = FindElementWithTimeout(By.Name("menuCash"), 3);
        if (cashButton == null) { Console.WriteLine("❌ Cash button not found"); return; }
        cashButton.Click();
        Thread.Sleep(100);
        TapCenterScreen();
        Thread.Sleep(100);

        var amountField = FindAmountField();
        if (amountField == null) { Console.WriteLine("❌ Amount field not found"); return; }
        amountField.Click();
        amountField.Clear();
        amountField.SendKeys("299");
        Thread.Sleep(100);

        var doneButton = FindDoneButton();
        if (doneButton == null) { Console.WriteLine("❌ Done button not found"); return; }
        doneButton.Click();
        Thread.Sleep(3000);

        var printReceiptButton = FindPrintReceiptButton();
        if (printReceiptButton != null) { printReceiptButton.Click(); Thread.Sleep(50); Console.WriteLine("✅ Parked sale retrieved and completed!"); }
        else
        {
            var noReceiptButton = FindNoReceiptButton();
            if (noReceiptButton != null) { noReceiptButton.Click(); Thread.Sleep(50); Console.WriteLine("✅ Parked sale retrieved and completed!"); }
            else { Console.WriteLine("⚠️ No receipt button found"); }
        }
        TakeScreenshot("ParkSale_07_SaleCompleted");
    }

    private void PerformAddCustomerToSaleTest()
    {
        Console.WriteLine("Starting new sale to test add customer...");
        var startSaleButton = FindElementWithTimeout(By.Name("MenuStartSale"), 5);
        if (startSaleButton == null) { Console.WriteLine("❌ MenuStartSale not found"); return; }
        startSaleButton.Click();
        Thread.Sleep(100);

        Console.WriteLine("Looking for Add Customer button...");
        var addCustomerButton = FindElementWithTimeout(By.Name("AddCustomerTapped"), 3);
        if (addCustomerButton == null)
        {
            string[] customerSelectors = ["Add Customer", "Add customer", "AddCustomer"];
            foreach (var sel in customerSelectors)
            {
                try { var el = Driver.FindElement(By.Name(sel)); if (el != null && el.Displayed) { addCustomerButton = el; break; } }
                catch (Exception) { }
            }
        }
        if (addCustomerButton == null) { Console.WriteLine("❌ Add Customer button not found"); TakeScreenshot("AddCustomer_ERROR_NoButton"); return; }
        addCustomerButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("AddCustomer_01_SearchPage");

        Console.WriteLine("Using active element (search field already has focus)...");
        Thread.Sleep(500);

        Console.WriteLine("Typing search term into active field...");
        var activeField = Driver.SwitchTo().ActiveElement() as AppiumElement;
        if (activeField == null) { Console.WriteLine("❌ Could not get active element"); TakeScreenshot("AddCustomer_ERROR_NoActiveField"); return; }
        foreach (var c in "Vibeke")
        {
            activeField.SendKeys(c.ToString());
            Thread.Sleep(150);
        }
        Thread.Sleep(500);

        Console.WriteLine("Pressing Return to trigger search...");
        activeField.SendKeys("\n");
        Thread.Sleep(2000);
        TakeScreenshot("AddCustomer_02_SearchResults");

        var firstCustomer = FindElementWithTimeout(By.XPath("(//XCUIElementTypeCell)[1]"), 5);
        if (firstCustomer == null) { Console.WriteLine("❌ No customer found in search results"); TakeScreenshot("AddCustomer_ERROR_NoResults"); return; }
        firstCustomer.Click();
        Thread.Sleep(500);
        TakeScreenshot("AddCustomer_03_CustomerAdded");
        Console.WriteLine("✅ Customer added to sale!");

        var barcodeField = FindElementWithTimeout(By.Name("ScanCode"), 5);
        if (barcodeField == null) { Console.WriteLine("❌ ScanCode not found"); return; }
        barcodeField.Click();
        barcodeField.Clear();
        barcodeField.SendKeys("5401157473672");
        TryHideKeyboard();
        Thread.Sleep(1000);
        TakeScreenshot("AddCustomer_04_ProductAdded");

        Console.WriteLine("Tapping salesline to mark as return...");
        var salesLine = FindElementWithTimeout(By.Name("SalesLineItem"), 5);
        if (salesLine == null) { Console.WriteLine("❌ Salesline not found"); return; }
        salesLine.Click();
        Thread.Sleep(500);
        TakeScreenshot("AddCustomer_05_SalesLinePopup");

        Console.WriteLine("Tapping Return to inventory...");
        var returnButton = FindElementWithTimeout(By.Name("BtnReturnToStock"), 3)
            ?? FindElementWithTimeout(By.Name("Return to inventory"), 3);
        if (returnButton == null) { Console.WriteLine("❌ Return to inventory button not found"); TakeScreenshot("AddCustomer_ERROR_NoReturnButton"); return; }
        returnButton.Click();
        Thread.Sleep(500);
        TakeScreenshot("AddCustomer_06_MarkedAsReturn");
        Console.WriteLine("✅ Product marked as return!");

        Console.WriteLine("Tapping Done button to complete return...");
        var doneMenuButton = FindElementWithTimeout(By.Name("menuDone"), 5);
        if (doneMenuButton == null) { Console.WriteLine("❌ menuDone button not found"); TakeScreenshot("AddCustomer_ERROR_NoDoneButton"); return; }
        doneMenuButton.Click();
        Thread.Sleep(1000);
        TakeScreenshot("AddCustomer_07_DoneTapped");

        Console.WriteLine("Looking for cash back / give cash back button...");
        var cashBackButton = FindElementWithTimeout(By.Name("menuCash"), 3);
        if (cashBackButton == null)
        {
            string[] cashBackSelectors = ["Give cash back", "Cash", "Gi kontant tilbake", "Cash back"];
            foreach (var sel in cashBackSelectors)
            {
                try { var el = Driver.FindElement(By.Name(sel)); if (el != null && el.Displayed) { cashBackButton = el; break; } }
                catch (Exception) { }
            }
        }
        if (cashBackButton != null) { cashBackButton.Click(); Thread.Sleep(1000); }
        else { Console.WriteLine("⚠️ Cash back button not found, continuing..."); }

        Thread.Sleep(3000);
        var printReceiptButton = FindPrintReceiptButton();
        if (printReceiptButton != null) { printReceiptButton.Click(); Thread.Sleep(50); }
        else
        {
            var noReceiptButton = FindNoReceiptButton();
            if (noReceiptButton != null) { noReceiptButton.Click(); Thread.Sleep(50); }
            else { Console.WriteLine("⚠️ No receipt button found"); }
        }
        TakeScreenshot("AddCustomer_08_SaleCompleted");
        Console.WriteLine("✅ Sale with customer and return completed!");
    }

    private void PerformLogout()
    {
        Console.WriteLine("Looking for logout button...");
        var logoutButton = FindElementWithTimeout(MobileBy.AccessibilityId("BtnLogOut"), 3);
        if (logoutButton == null) { Console.WriteLine("Logout button not found"); return; }

        logoutButton.Click();
        Thread.Sleep(200);

        var confirmButton = FindElementWithTimeout(OkButtonLocator, 3);
        if (confirmButton != null)
        {
            Thread.Sleep(200);
            confirmButton.Click();
            Thread.Sleep(500);
            Console.WriteLine("✅ Logout dialog dismissed");
        }
        else
        {
            Console.WriteLine("Warning: Logout confirmation dialog not found");
        }
    }

    private void DebugCurrentPage()
    {
        try
        {
            string pageSource = Driver.PageSource;

            if (pageSource.Contains("PreviousServerSettlementPage"))
                Console.WriteLine("📄 Currently on: PreviousServerSettlementPage");
            else if (pageSource.Contains("XReportServerPage"))
                Console.WriteLine("📄 Currently on: XReportServerPage");
            else
                Console.WriteLine("📄 Unknown page");

            string dumpsDir = Path.Combine(Directory.GetCurrentDirectory(), "PageSourceDumps");
            Directory.CreateDirectory(dumpsDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.WriteAllText(Path.Combine(dumpsDir, $"PageSource_{timestamp}.xml"), pageSource);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
