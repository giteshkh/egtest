const { Builder } = require('selenium-webdriver')
const { expect } = require('chai')
const data = require('../data/text-input.json')
const { UiElementsPage, TextEditorPage, DEFAULT_TIMEOUT } = require('../pageobjects/uielements.page.js')

describe('BrowserStack Sample iOS - Text input', function () {
  let driver

  // browserstack-node-sdk injects the hub, credentials, device and app from browserstack.yml
  beforeEach(async function () {
    // No browserName: this is a native app session (device, OS and app come from browserstack.yml)
    driver = await new Builder()
      .withCapabilities({ platformName: 'iOS', 'appium:automationName': 'XCUITest' })
      .build()
  })

  afterEach(async function () {
    if (driver) {
      await driver.quit()
    }
  })

  it('shows the entered text in the output label', async function () {
    const uiElements = new UiElementsPage(driver)
    const textEditor = new TextEditorPage(driver)

    await uiElements.tapTextButton()
    await textEditor.submitText(data.inputText)

    await driver.wait(
      async () => (await textEditor.getOutputText()) === data.inputText,
      DEFAULT_TIMEOUT,
      `Text Output should show "${data.inputText}" after submitting the text`
    )
    expect(
      await textEditor.getOutputText(),
      `Text Output should equal the entered text "${data.inputText}"`
    ).to.equal(data.inputText)
  })
})
