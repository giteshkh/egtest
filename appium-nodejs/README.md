# Appium (Node.js) on BrowserStack

Mocha + Appium (via the Selenium WebDriver client) run on BrowserStack App Automate with the
**BrowserStack Node SDK**. Devices, app, reporting and debug features are configured in
`browserstack.yml`, not in the test code, so scaling to more devices is a config change.

```
appium-nodejs/
├── browserstack.yml                      # devices, app, SDK features
├── .env.example                          # copy to .env (git-ignored)
└── test/
    ├── data/                             # test data (no values inside test code)
    ├── pageobjects/
    │   ├── uielements.page.js            # sample app page objects
    │   └── customer.pos.page.js          # your POS app, translated from the C# suite
    └── specs/
        ├── text-input.spec.js            # sample app test
        └── customer-complete-flow.spec.js# your tests, translated from the C# suite
```

## 1. Try the sample app test (5 minutes)

1. **Install** (Node 18+):
   ```bash
   npm install
   ```
2. **Add credentials.** Copy `.env.example` to `.env` and fill in `BROWSERSTACK_USERNAME` and
   `BROWSERSTACK_ACCESS_KEY` (find them under *Account > Settings* on BrowserStack).
3. **Upload the sample app** (the copy kept in this repo at `../sample-app/BStackSampleApp.ipa`):
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@../sample-app/BStackSampleApp.ipa"
   ```
   Copy the `app_url` from the response (it looks like `bs://abc123...`).
4. **Point the tests at it.** Set `BS_APP_ID=bs://abc123...` in `.env`.
5. **Run:**
   ```bash
   npm run test:sample
   ```
   The test runs in parallel on the devices listed in `browserstack.yml`. The build link is printed at
   the end of the run (App Automate dashboard and Test Reporting & Analytics).

## 2. Run your own tests

`test/specs/customer-complete-flow.spec.js` and `test/pageobjects/customer.pos.page.js` are a
translation of the C# suite (`LoginFlowTest`, `SaleTest`, `CompleteFlowTest` and their helpers):
login, permission dialogs, unlock, cash sale, product search, delete line and cancel sale, park and
retrieve a sale, add customer and return, Z-report settlement, logout.

1. **Upload your app** (`.ipa`) the same way, with a file instead of a URL:
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@/path/to/your-app.ipa"
   ```
2. **Update the app URL:** set `BS_APP_ID` in `.env` to the new `bs://...` value (no code changes).
3. **Add your test account** to `.env`: `TEST_SECONDARY_USERNAME`, `TEST_SECONDARY_PASSWORD`,
   `FS_UNLOCK_NAME`, `FS_UNLOCK_PIN`. These are the same variables the C# suite reads. The customer
   spec is skipped until they are all set.
4. **Run:**
   ```bash
   npm run test:customer
   ```

**Note:** the translated locators come straight from the C# suite and were not run against your app
here. Expect a first run to need a few locator tweaks in `test/pageobjects/customer.pos.page.js`.
Product codes, amounts and the search term live in `test/data/customer-pos.json`.

## Scaling and features

Everything below is a setting in `browserstack.yml`:

| Goal | Setting |
|---|---|
| More devices / OS versions | add entries under `platforms` |
| More parallel threads | `parallelsPerPlatform` |
| Reporting and flaky-test analytics | `testReporting` |
| Video, network, device and Appium logs | `video`, `networkLogs`, `deviceLogs`, `appiumLogs` |
| App performance | `appProfiling` |
| Self-healing locators | `selfHeal` |
| Accessibility scan | `accessibility` |
