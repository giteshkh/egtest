using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace BStackAppiumNUnit.Pages;

/// <summary>
/// Page object for the customer's POS app. Locators are carried over from the customer's own suite
/// (LoginHelper, PermissionsHelper, BaseIosTest, CompleteFlowTest). They have NOT been re-verified
/// against the customer's app here: validate them on a first run.
/// </summary>
public class CustomerPosPage
{
    private const int DefaultTimeoutSeconds = 10;
    private readonly IWebDriver _driver;

    public CustomerPosPage(IWebDriver driver) => _driver = driver;

    private static By Acc(string id) => MobileBy.AccessibilityId(id);
    private static By NameIs(string name) => By.XPath($"//*[@name=\"{name}\"]");

    // ---- generic helpers (explicit waits only, default 10s) ----
    private IWebElement Find(By locator, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds))
        {
            Message = $"Element {locator} was not found within {timeoutSeconds}s"
        };
        return wait.Until(d => d.FindElement(locator));
    }

    private IWebElement? TryFind(By locator, int timeoutSeconds = 2)
    {
        try { return Find(locator, timeoutSeconds); }
        catch (WebDriverTimeoutException) { return null; }
    }

    private IWebElement? FirstOf(By[] locators, int timeoutSeconds = 2)
    {
        foreach (var locator in locators)
        {
            var el = TryFind(locator, timeoutSeconds);
            if (el != null) return el;
        }
        return null;
    }

    private static void TypeInto(IWebElement element, string value)
    {
        element.Click();
        element.Clear();
        element.SendKeys(value);
    }

    private void HideKeyboard()
    {
        FirstOf([NameIs("return"), NameIs("Return"), NameIs("Done"), NameIs("Search")], 1)?.Click();
    }

    public void AcceptSystemAlertIfPresent()
    {
        try { _driver.SwitchTo().Alert().Accept(); }
        catch (NoAlertPresentException) { }
    }

    public void DismissOkPopupIfPresent()
    {
        TryFind(By.XPath("//XCUIElementTypeButton[@label='OK' or @name='OK' or @name='BtnConfirmOkButton' or @name='BtnAlertOkButton' or @name='BtnDialogOkClicked']"), 1)?.Click();
    }

    // ---- login (LoginHelper.PerformLogin) ----
    public bool IsPosReady(int timeoutSeconds = 1) =>
        FirstOf([Acc("MenuStartSale"), By.XPath("//XCUIElementTypeStaticText[@name='New sale']")], timeoutSeconds) != null;

    public void Login(string username, string password)
    {
        if (IsPosReady(2)) return;

        // Onboarding: tap the install / next button to reveal the login form
        FirstOf(
        [
            Acc("BtnNextLoggInn"),
            Acc("btnScannerStepNext"),
            By.XPath("//*[contains(@name, 'Install') or contains(@label, 'Install') or contains(@value, 'Install')]")
        ], 5)?.Click();
        TryFind(Acc("btnScannerStepNext"), 1)?.Click();

        var userField = FirstOf(
        [
            Acc("AutUserName"),
            By.XPath("//XCUIElementTypeTextField[contains(@name, 'User')]"),
            By.XPath("//XCUIElementTypeTextField[1]")
        ], DefaultTimeoutSeconds) ?? throw new InvalidOperationException("Username field not found after trying multiple locators");
        userField.Clear();
        userField.SendKeys(username);
        HideKeyboard();

        TypeInto(Find(By.XPath("//XCUIElementTypeSecureTextField")), password);
        HideKeyboard();

        Find(By.XPath("//XCUIElementTypeButton[contains(@name, 'Log in') or contains(@name, 'Logg') or @name='btnLoggInn']")).Click();
    }

    // ---- iOS permission dialogs (PermissionsHelper.HandleIOSPermissions) ----
    public void HandlePermissions(int timeoutSeconds = 60)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var alerts = _driver.FindElements(By.XPath("//XCUIElementTypeAlert"));
            if (alerts.Count == 0) return;

            var buttons = alerts[0].FindElements(By.XPath(".//XCUIElementTypeButton[contains(@name, 'Allow') or contains(@label, 'Allow')]"));
            if (buttons.Count == 0)
                buttons = alerts[0].FindElements(By.XPath(".//XCUIElementTypeButton[contains(@name, 'OK') or contains(@label, 'OK')]"));
            if (buttons.Count == 0) return;
            buttons[0].Click();
        }
    }

    // ---- unlock (BaseIosTest.EnsureUnlocked) ----
    public void EnsureUnlocked(string displayName, string pin)
    {
        if (IsPosReady(2)) return;
        TryFind(By.XPath($"//XCUIElementTypeStaticText[contains(@name,\"{displayName}\")]"), 5)?.Click();
        foreach (var digit in pin)
            Find(By.XPath($"//XCUIElementTypeButton[@label=\"{digit}\" or @name=\"{digit}\"]"), 5).Click();
        Find(Acc("MenuStartSale"), 30);
    }

    // ---- sale (BaseIosTest.PerformSingleSale) ----
    public void StartSale() => Find(Acc("MenuStartSale")).Click();

    public void ScanBarcode(string barcode)
    {
        TypeInto(Find(Acc("ScanCode")), barcode);
        HideKeyboard();
    }

    public void PayCash(string amount)
    {
        Find(Acc("menuCash")).Click();
        var amountField = FirstOf([Acc("Amount"), By.XPath("//XCUIElementTypeTextField")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Amount field not found");
        TypeInto(amountField, amount);
        var done = FirstOf([Acc("BtnPaymentDone"), Acc("BtnDiscountDone"), Acc("Done"), Acc("OK"), Acc("BtnDone")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Done button not found");
        done.Click();
    }

    public void FinishReceiptScreen()
    {
        FirstOf([Acc("BtnNoReceipt"), Acc("BtnReceipt")], DefaultTimeoutSeconds)?.Click();
        Find(Acc("MenuStartSale"), 15);
    }

    // ---- product search (SaleTest) ----
    public void SearchAndSelectProduct(string code, string productName)
    {
        Find(NameIs("Search")).Click();
        TypeInto(Find(NameIs("EntrySearch")), code);
        HideKeyboard();
        Find(By.XPath($"//*[contains(@name, \"{productName}\") or contains(@label, \"{productName}\")]")).Click();
    }

    // ---- delete salesline and cancel sale ----
    public void DeleteFirstSalesLine()
    {
        var line = FirstOf([NameIs("SalesLineItem"), By.XPath("(//XCUIElementTypeCell)[1]")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Salesline element not found");
        line.Click();
        var del = FirstOf([NameIs("BtnDeleteSalesLine"), NameIs("Delete"), NameIs("Slett")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Delete button not found");
        del.Click();
    }

    public void CancelSale()
    {
        Find(NameIs("btnMore")).Click();
        var cancel = FirstOf([NameIs("BtnCancelSale"), NameIs("Cancel sale")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Cancel sale button not found");
        cancel.Click();
    }

    // ---- park and retrieve sale ----
    public void ParkSale()
    {
        Find(NameIs("btnMore")).Click();
        Find(NameIs("Park sale")).Click();
    }

    public void OpenParkedSales()
    {
        Find(NameIs("More")).Click();
        var parked = FirstOf([NameIs("BtnParkedSales"), NameIs("Parked")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Parked sales button not found");
        parked.Click();
        Find(By.XPath("(//XCUIElementTypeCell)[1]")).Click();
    }

    // ---- add customer and return ----
    public void AddCustomer(string searchTerm)
    {
        var add = FirstOf([NameIs("AddCustomerTapped"), NameIs("Add Customer"), NameIs("Add customer"), NameIs("AddCustomer")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Add Customer button not found");
        add.Click();
        var active = _driver.SwitchTo().ActiveElement();
        active.SendKeys(searchTerm);
        active.SendKeys("\n");
        Find(By.XPath("(//XCUIElementTypeCell)[1]")).Click();
    }

    public void MarkFirstLineAsReturn()
    {
        Find(NameIs("SalesLineItem")).Click();
        var ret = FirstOf([NameIs("BtnReturnToStock"), NameIs("Return to inventory")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Return to inventory button not found");
        ret.Click();
        Find(NameIs("menuDone")).Click();
    }

    public void GiveCashBackIfPrompted() =>
        FirstOf([NameIs("menuCash"), NameIs("Cash"), NameIs("Give cash back")], 3)?.Click();

    // ---- Z-report settlement (BaseIosTest.PerformZReportSettlement) ----
    public void OpenZReport()
    {
        var more = FirstOf([NameIs("More"), Acc("More"), Acc("btnMore")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("More button not found: cannot start Z-Report settlement");
        more.Click();
        var z = FirstOf([NameIs("Z Report"), NameIs("Z-Report"), Acc("SettlementZReportButton"), Acc("Settlement Z-Report")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Z-Report option not found in popup");
        z.Click();
    }

    public void Withdraw(string amount)
    {
        Find(Acc("BtnAddWithdrawal"), 30).Click();
        var field = FirstOf([Acc("Amount"), By.XPath("//XCUIElementTypeTextField")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Amount field not found during withdrawal");
        TypeInto(field, amount);
        Find(Acc("BtnWithdrawalWithdraw")).Click();
    }

    public void EnterCashInPos(string amount)
    {
        var field = FirstOf([Acc("CashHoldings"), Acc("Cash in POS")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Cash in POS field not found");
        TypeInto(field, amount);
        Find(Acc("Done")).Click();
    }

    public void ConfirmSettlement()
    {
        Find(Acc("BtnSettlement")).Click();
        var yes = FirstOf([Acc("BtnConfirmOkButton"), Acc("Yes"), Acc("OK")], DefaultTimeoutSeconds)
            ?? throw new InvalidOperationException("Yes button not found on settlement confirmation popup");
        yes.Click();
    }

    // ---- navigation / logout ----
    public void NavigateBack() => FirstOf([Acc("BackButton"), Acc("Back"), Acc("BtnBack")], 3)?.Click();

    public void Logout()
    {
        Find(Acc("BtnLogOut"), 3).Click();
        TryFind(By.XPath("//XCUIElementTypeButton[@label='OK' or @name='OK' or @name='BtnConfirmOkButton' or @name='BtnAlertOkButton' or @name='BtnDialogOkClicked']"), 3)?.Click();
    }
}
