// Translated from FrontSystems.MobileAutomation.iOS (CompleteFlowTest, SaleTest, LoginFlowTest).
// Runs against the CUSTOMER'S app: set BS_APP_ID to their uploaded app (bs://...) in .env.
// Skipped automatically until the customer's credentials are provided.
const { Builder } = require('selenium-webdriver')
const { expect } = require('chai')
const data = require('../data/customer-pos.json')
const { CustomerPosPage } = require('../pageobjects/customer.pos.page.js')

const username = process.env.TEST_SECONDARY_USERNAME
const password = process.env.TEST_SECONDARY_PASSWORD
const unlockName = process.env.FS_UNLOCK_NAME
const unlockPin = process.env.FS_UNLOCK_PIN

const describeIfConfigured = username && password && unlockName && unlockPin ? describe : describe.skip

describeIfConfigured('Customer POS - iOS', function () {
  let driver
  let pos

  beforeEach(async function () {
    // Native app session: device, OS and app come from browserstack.yml
    driver = await new Builder()
      .withCapabilities({ platformName: 'iOS', 'appium:automationName': 'XCUITest', 'appium:autoAcceptAlerts': true })
      .build()
    pos = new CustomerPosPage(driver)
    await pos.login(username, password)
    await pos.handlePermissions()
    await pos.ensureUnlocked(unlockName, unlockPin)
  })

  afterEach(async function () {
    if (driver) await driver.quit()
  })

  it('creates a cash sale and finishes on the home screen (LoginFlow + Sale)', async function () {
    await pos.acceptSystemAlertIfPresent()
    await pos.dismissOkPopupIfPresent()
    await pos.startSale()
    await pos.scanBarcode(data.saleBarcode)
    await pos.payCash(data.cashAmount)
    await pos.finishReceiptScreen()
    expect(await pos.isPosReady(10000), 'POS home screen (MenuStartSale) should be shown after the sale is finished').to.equal(true)
  })

  it('finds a product by code in the search results (SaleTest)', async function () {
    await pos.startSale()
    await pos.searchAndSelectProduct(data.searchProductCode, data.searchProductName)
  })

  it('deletes a sales line and cancels the sale', async function () {
    await pos.startSale()
    await pos.scanBarcode(data.saleBarcode)
    await pos.deleteFirstSalesLine()
    await pos.scanBarcode(data.saleBarcode)
    await pos.cancelSale()
    expect(await pos.isPosReady(10000), 'POS home screen should be shown after the sale is cancelled').to.equal(true)
  })

  it('parks a sale and retrieves it from the parked list', async function () {
    await pos.startSale()
    await pos.scanBarcode(data.saleBarcode)
    await pos.parkSale()
    await pos.openParkedSales()
    await pos.payCash(data.cashAmount)
    await pos.finishReceiptScreen()
    expect(await pos.isPosReady(10000), 'POS home screen should be shown after the parked sale is completed').to.equal(true)
  })

  it('adds a customer to a sale and completes it as a return', async function () {
    await pos.startSale()
    await pos.addCustomer(data.customerSearchTerm)
    await pos.scanBarcode(data.saleBarcode)
    await pos.markFirstLineAsReturn()
    await pos.giveCashBackIfPrompted()
    await pos.finishReceiptScreen()
    expect(await pos.isPosReady(10000), 'POS home screen should be shown after the return is completed').to.equal(true)
  })

  it('settles the day with a Z-report and logs out', async function () {
    await pos.openZReport()
    await pos.withdraw(data.cashAmount)
    await pos.enterCashInPos(data.cashInPos)
    await pos.confirmSettlement()
    await pos.navigateBack()
    await pos.logout()
  })
})
