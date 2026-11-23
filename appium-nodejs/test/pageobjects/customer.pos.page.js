// Page object for the customer's POS app (translated from FrontSystems.MobileAutomation.iOS C# helpers).
// Locators mirror the customer's C# suite (LoginHelper, BaseIosTest, CompleteFlowTest, SaleTest).
// They have NOT been re-verified against the customer's app: validate them on a first run.
const { By, until } = require('selenium-webdriver')

const DEFAULT_TIMEOUT = 10000

const acc = (id) => new By('accessibility id', id)
const nameIs = (name) => By.xpath(`//*[@name="${name}"]`)

class CustomerPosPage {
  constructor(driver) {
    this.driver = driver
  }

  // ---- generic helpers (explicit waits only, default 10s) ----
  async find(locator, timeout = DEFAULT_TIMEOUT) {
    return this.driver.wait(until.elementLocated(locator), timeout, `Element ${locator} was not found within ${timeout}ms`)
  }

  async tryFind(locator, timeout = 2000) {
    try {
      return await this.driver.wait(until.elementLocated(locator), timeout)
    } catch (e) {
      return null
    }
  }

  async firstOf(locators, timeout = 2000) {
    for (const locator of locators) {
      const el = await this.tryFind(locator, timeout)
      if (el) return el
    }
    return null
  }

  async typeInto(element, value) {
    await element.click()
    await element.clear()
    await element.sendKeys(value)
  }

  async hideKeyboard() {
    const key = await this.firstOf([nameIs('return'), nameIs('Return'), nameIs('Done'), nameIs('Search')], 1000)
    if (key) await key.click()
  }

  async acceptSystemAlertIfPresent() {
    try {
      await this.driver.switchTo().alert().accept()
    } catch (e) {
      // no alert present
    }
  }

  async dismissOkPopupIfPresent() {
    const ok = await this.tryFind(
      By.xpath("//XCUIElementTypeButton[@label='OK' or @name='OK' or @name='BtnConfirmOkButton' or @name='BtnAlertOkButton' or @name='BtnDialogOkClicked']"),
      1000
    )
    if (ok) await ok.click()
  }

  // ---- login (LoginHelper.PerformLogin) ----
  async isPosReady(timeout = 1000) {
    const el = await this.firstOf([acc('MenuStartSale'), By.xpath("//XCUIElementTypeStaticText[@name='New sale']")], timeout)
    return el !== null
  }

  async login(username, password) {
    if (await this.isPosReady(2000)) return

    // Onboarding: tap the install / next button to reveal the login form
    const install = await this.firstOf(
      [
        acc('BtnNextLoggInn'),
        acc('btnScannerStepNext'),
        By.xpath("//*[contains(@name, 'Install') or contains(@label, 'Install') or contains(@value, 'Install')]")
      ],
      5000
    )
    if (install) await install.click()

    const scannerNext = await this.tryFind(acc('btnScannerStepNext'), 1000)
    if (scannerNext) await scannerNext.click()

    const userField = await this.firstOf(
      [
        acc('AutUserName'),
        By.xpath("//XCUIElementTypeTextField[contains(@name, 'User')]"),
        By.xpath('//XCUIElementTypeTextField[1]')
      ],
      DEFAULT_TIMEOUT
    )
    if (!userField) throw new Error('Username field not found after trying multiple locators')
    await userField.clear()
    await userField.sendKeys(username)
    await this.hideKeyboard()

    const passwordField = await this.find(By.xpath('//XCUIElementTypeSecureTextField'))
    await this.typeInto(passwordField, password)
    await this.hideKeyboard()

    const loginButton = await this.find(
      By.xpath("//XCUIElementTypeButton[contains(@name, 'Log in') or contains(@name, 'Logg') or @name='btnLoggInn']")
    )
    await loginButton.click()
  }

  // ---- iOS permission dialogs (PermissionsHelper.HandleIOSPermissions) ----
  async handlePermissions(timeoutMs = 60000) {
    const deadline = Date.now() + timeoutMs
    while (Date.now() < deadline) {
      const alerts = await this.driver.findElements(By.xpath('//XCUIElementTypeAlert'))
      if (alerts.length === 0) return
      // Same xpaths as PermissionsHelper.ClickIOSAllowButton / ClickIOSOKButton in the C# suite
      let buttons = await alerts[0].findElements(
        By.xpath(".//XCUIElementTypeButton[contains(@name, 'Allow') or contains(@label, 'Allow')]")
      )
      if (buttons.length === 0) {
        buttons = await alerts[0].findElements(
          By.xpath(".//XCUIElementTypeButton[contains(@name, 'OK') or contains(@label, 'OK')]")
        )
      }
      if (buttons.length === 0) return
      await buttons[0].click()
    }
  }

  // ---- unlock (BaseIosTest.EnsureUnlocked) ----
  async ensureUnlocked(displayName, pin) {
    if (await this.isPosReady(2000)) return
    const user = await this.tryFind(By.xpath(`//XCUIElementTypeStaticText[contains(@name,"${displayName}")]`), 5000)
    if (user) await user.click()
    for (const digit of pin) {
      const key = await this.find(By.xpath(`//XCUIElementTypeButton[@label="${digit}" or @name="${digit}"]`), 5000)
      await key.click()
    }
    await this.find(acc('MenuStartSale'), 30000)
  }

  // ---- sale (BaseIosTest.PerformSingleSale) ----
  async startSale() {
    await (await this.find(acc('MenuStartSale'))).click()
  }

  async scanBarcode(barcode) {
    const field = await this.find(acc('ScanCode'))
    await this.typeInto(field, barcode)
    await this.hideKeyboard()
  }

  async payCash(amount) {
    await (await this.find(acc('menuCash'))).click()
    const amountField = await this.firstOf(
      [acc('Amount'), By.xpath('//XCUIElementTypeTextField')],
      DEFAULT_TIMEOUT
    )
    if (!amountField) throw new Error('Amount field not found')
    await this.typeInto(amountField, amount)
    const done = await this.firstOf(
      [acc('BtnPaymentDone'), acc('BtnDiscountDone'), acc('Done'), acc('OK'), acc('BtnDone')],
      DEFAULT_TIMEOUT
    )
    if (!done) throw new Error('Done button not found')
    await done.click()
  }

  async finishReceiptScreen() {
    const receipt = await this.firstOf([acc('BtnNoReceipt'), acc('BtnReceipt')], DEFAULT_TIMEOUT)
    if (receipt) await receipt.click()
    await this.find(acc('MenuStartSale'), 15000)
  }

  // ---- product search (SaleTest) ----
  async searchAndSelectProduct(code, productName) {
    await (await this.find(nameIs('Search'))).click()
    const entry = await this.find(nameIs('EntrySearch'))
    await this.typeInto(entry, code)
    await this.hideKeyboard()
    const product = await this.find(By.xpath(`//*[contains(@name, "${productName}") or contains(@label, "${productName}")]`))
    await product.click()
  }

  // ---- delete salesline and cancel sale (CompleteFlowTest.PerformDeleteSaleslineAndCancelTest) ----
  async deleteFirstSalesLine() {
    const line = await this.firstOf([nameIs('SalesLineItem'), By.xpath('(//XCUIElementTypeCell)[1]')], DEFAULT_TIMEOUT)
    if (!line) throw new Error('Salesline element not found')
    await line.click()
    const del = await this.firstOf([nameIs('BtnDeleteSalesLine'), nameIs('Delete'), nameIs('Slett')], DEFAULT_TIMEOUT)
    if (!del) throw new Error('Delete button not found')
    await del.click()
  }

  async cancelSale() {
    await (await this.find(nameIs('btnMore'))).click()
    const cancel = await this.firstOf([nameIs('BtnCancelSale'), nameIs('Cancel sale')], DEFAULT_TIMEOUT)
    if (!cancel) throw new Error('Cancel sale button not found')
    await cancel.click()
  }

  // ---- park and retrieve sale (CompleteFlowTest.PerformParkSaleTest) ----
  async parkSale() {
    await (await this.find(nameIs('btnMore'))).click()
    await (await this.find(nameIs('Park sale'))).click()
  }

  async openParkedSales() {
    await (await this.find(nameIs('More'))).click()
    const parked = await this.firstOf([nameIs('BtnParkedSales'), nameIs('Parked')], DEFAULT_TIMEOUT)
    if (!parked) throw new Error('Parked sales button not found')
    await parked.click()
    await (await this.find(By.xpath('(//XCUIElementTypeCell)[1]'))).click()
  }

  // ---- add customer and return (CompleteFlowTest.PerformAddCustomerToSaleTest) ----
  async addCustomer(searchTerm) {
    const add = await this.firstOf(
      [nameIs('AddCustomerTapped'), nameIs('Add Customer'), nameIs('Add customer'), nameIs('AddCustomer')],
      DEFAULT_TIMEOUT
    )
    if (!add) throw new Error('Add Customer button not found')
    await add.click()
    const active = await this.driver.switchTo().activeElement()
    await active.sendKeys(searchTerm)
    await active.sendKeys('\n')
    await (await this.find(By.xpath('(//XCUIElementTypeCell)[1]'))).click()
  }

  async markFirstLineAsReturn() {
    await (await this.find(nameIs('SalesLineItem'))).click()
    const ret = await this.firstOf([nameIs('BtnReturnToStock'), nameIs('Return to inventory')], DEFAULT_TIMEOUT)
    if (!ret) throw new Error('Return to inventory button not found')
    await ret.click()
    await (await this.find(nameIs('menuDone'))).click()
  }

  async giveCashBackIfPrompted() {
    const cash = await this.firstOf([nameIs('menuCash'), nameIs('Cash'), nameIs('Give cash back')], 3000)
    if (cash) await cash.click()
  }

  // ---- Z-report settlement (BaseIosTest.PerformZReportSettlement) ----
  async openZReport() {
    const more = await this.firstOf([nameIs('More'), acc('More'), acc('btnMore')], DEFAULT_TIMEOUT)
    if (!more) throw new Error('More button not found: cannot start Z-Report settlement')
    await more.click()
    const z = await this.firstOf(
      [nameIs('Z Report'), nameIs('Z-Report'), acc('SettlementZReportButton'), acc('Settlement Z-Report')],
      DEFAULT_TIMEOUT
    )
    if (!z) throw new Error('Z-Report option not found in popup')
    await z.click()
  }

  async withdraw(amount) {
    await (await this.find(acc('BtnAddWithdrawal'), 30000)).click()
    const field = await this.firstOf([acc('Amount'), By.xpath('//XCUIElementTypeTextField')], DEFAULT_TIMEOUT)
    if (!field) throw new Error('Amount field not found during withdrawal')
    await this.typeInto(field, amount)
    await (await this.find(acc('BtnWithdrawalWithdraw'))).click()
  }

  async enterCashInPos(amount) {
    const field = await this.firstOf([acc('CashHoldings'), acc('Cash in POS')], DEFAULT_TIMEOUT)
    if (!field) throw new Error('Cash in POS field not found')
    await this.typeInto(field, amount)
    await (await this.find(acc('Done'))).click()
  }

  async confirmSettlement() {
    await (await this.find(acc('BtnSettlement'))).click()
    const yes = await this.firstOf([acc('BtnConfirmOkButton'), acc('Yes'), acc('OK')], DEFAULT_TIMEOUT)
    if (!yes) throw new Error('Yes button not found on settlement confirmation popup')
    await yes.click()
  }

  // ---- navigation / logout ----
  async navigateBack() {
    const back = await this.firstOf([acc('BackButton'), acc('Back'), acc('BtnBack')], 3000)
    if (back) await back.click()
  }

  async logout() {
    await (await this.find(acc('BtnLogOut'), 3000)).click()
    const confirm = await this.tryFind(
      By.xpath("//XCUIElementTypeButton[@label='OK' or @name='OK' or @name='BtnConfirmOkButton' or @name='BtnAlertOkButton' or @name='BtnDialogOkClicked']"),
      3000
    )
    if (confirm) await confirm.click()
  }
}

module.exports = { CustomerPosPage, DEFAULT_TIMEOUT }
