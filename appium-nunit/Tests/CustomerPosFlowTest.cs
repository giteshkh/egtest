using BStackAppiumNUnit.Helpers;
using BStackAppiumNUnit.Pages;
using Newtonsoft.Json.Linq;

namespace BStackAppiumNUnit.Tests;

/// <summary>
/// The customer's tests (LoginFlowTest, SaleTest, CompleteFlowTest) split into independent tests.
/// Run against the CUSTOMER'S app: set BS_APP_ID to their uploaded app. Skipped until the
/// test account variables are set (same names the customer's suite reads).
/// </summary>
[TestFixture]
[Category("Customer")]
public class CustomerPosFlowTest : BaseTest
{
    private static readonly string? Username = Environment.GetEnvironmentVariable("TEST_SECONDARY_USERNAME");
    private static readonly string? Password = Environment.GetEnvironmentVariable("TEST_SECONDARY_PASSWORD");
    private static readonly string? UnlockName = Environment.GetEnvironmentVariable("FS_UNLOCK_NAME");
    private static readonly string? UnlockPin = Environment.GetEnvironmentVariable("FS_UNLOCK_PIN");

    private CustomerPosPage _pos = null!;
    private JObject _data = null!;

    [SetUp]
    public void LoginAndUnlock()
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password) ||
            string.IsNullOrEmpty(UnlockName) || string.IsNullOrEmpty(UnlockPin))
        {
            Assert.Ignore("Customer test account not configured: set TEST_SECONDARY_USERNAME, TEST_SECONDARY_PASSWORD, FS_UNLOCK_NAME and FS_UNLOCK_PIN.");
        }

        _data = JObject.Parse(File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "customer-pos.json")));
        _pos = new CustomerPosPage(Driver);
        _pos.Login(Username, Password);
        _pos.HandlePermissions();
        _pos.EnsureUnlocked(UnlockName, UnlockPin);
    }

    private string Data(string key) => _data[key]!.Value<string>()!;

    [Test]
    public void Should_CreateCashSale_AndReturnToHome()
    {
        _pos.AcceptSystemAlertIfPresent();
        _pos.DismissOkPopupIfPresent();
        _pos.StartSale();
        _pos.ScanBarcode(Data("saleBarcode"));
        _pos.PayCash(Data("cashAmount"));
        _pos.FinishReceiptScreen();
        Assert.That(_pos.IsPosReady(10), Is.True, "POS home screen (MenuStartSale) should be shown after the sale is finished");
    }

    [Test]
    public void Should_FindProductByCode_InSearchResults()
    {
        _pos.StartSale();
        Assert.DoesNotThrow(() => _pos.SearchAndSelectProduct(Data("searchProductCode"), Data("searchProductName")),
            "The product should be found in the search results and be selectable");
    }

    [Test]
    public void Should_DeleteSalesLine_AndCancelSale()
    {
        _pos.StartSale();
        _pos.ScanBarcode(Data("saleBarcode"));
        _pos.DeleteFirstSalesLine();
        _pos.ScanBarcode(Data("saleBarcode"));
        _pos.CancelSale();
        Assert.That(_pos.IsPosReady(10), Is.True, "POS home screen should be shown after the sale is cancelled");
    }

    [Test]
    public void Should_ParkSale_AndRetrieveIt()
    {
        _pos.StartSale();
        _pos.ScanBarcode(Data("saleBarcode"));
        _pos.ParkSale();
        _pos.OpenParkedSales();
        _pos.PayCash(Data("cashAmount"));
        _pos.FinishReceiptScreen();
        Assert.That(_pos.IsPosReady(10), Is.True, "POS home screen should be shown after the parked sale is completed");
    }

    [Test]
    public void Should_AddCustomer_AndCompleteReturn()
    {
        _pos.StartSale();
        _pos.AddCustomer(Data("customerSearchTerm"));
        _pos.ScanBarcode(Data("saleBarcode"));
        _pos.MarkFirstLineAsReturn();
        _pos.GiveCashBackIfPrompted();
        _pos.FinishReceiptScreen();
        Assert.That(_pos.IsPosReady(10), Is.True, "POS home screen should be shown after the return is completed");
    }

    [Test]
    public void Should_SettleZReport_AndLogout()
    {
        _pos.OpenZReport();
        _pos.Withdraw(Data("cashAmount"));
        _pos.EnterCashInPos(Data("cashInPos"));
        _pos.ConfirmSettlement();
        _pos.NavigateBack();
        Assert.DoesNotThrow(() => _pos.Logout(), "Logout should complete without errors");
    }
}
