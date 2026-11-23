// Page objects for the BrowserStack Sample iOS app (selenium-webdriver + Appium XCUITest).
// Locators are accessibility ids verified on a live iOS session.
const { By, until } = require('selenium-webdriver')

const DEFAULT_TIMEOUT = 10000

class BasePage {
  constructor(driver) {
    this.driver = driver
  }

  async waitFor(accessibilityId) {
    // Appium accessibility id strategy (By.css would run JavaScript, which native apps do not support)
    const locator = new By('accessibility id', accessibilityId)
    return this.driver.wait(until.elementLocated(locator), DEFAULT_TIMEOUT,
      `Element "${accessibilityId}" was not found within ${DEFAULT_TIMEOUT}ms`)
  }
}

class UiElementsPage extends BasePage {
  async tapTextButton() {
    // elementIsVisible runs browser JavaScript, which a native app does not support
    const el = await this.waitFor('Text Button')
    await el.click()
  }
}

class TextEditorPage extends BasePage {
  async submitText(value) {
    const input = await this.waitFor('Text Input')
    await input.sendKeys(value)
    const returnKey = await this.waitFor('Return')
    await returnKey.click()
  }

  async getOutputText() {
    const output = await this.waitFor('Text Output')
    return output.getText()
  }
}

module.exports = { UiElementsPage, TextEditorPage, DEFAULT_TIMEOUT }
