# Front Systems: Mobile test automation on BrowserStack

This repo shows how your existing iOS test suite moves to BrowserStack App Automate in three frameworks,
and how the same tests scale across real devices without changing the test logic.

You get three ready-to-run folders next to your original tests. Each one has a sample test that runs
against BrowserStack's sample app (so you can see a green run in minutes) and a translation of your own
POS tests that you point at your own app.

## What is in this repo

| Folder | What it is |
|---|---|
| `FrontSystems.MobileAutomation.iOS/` | **Your original C# / NUnit tests, untouched.** |
| `appium-nunit/` | The same Appium + NUnit approach, set up to run on BrowserStack with the BrowserStack .NET SDK. |
| `appium-nodejs/` | Appium with Node.js (Mocha), set up to run on BrowserStack with the BrowserStack Node SDK. |
| `maestro/` | Maestro flows that run on BrowserStack real devices. |
| `sample-app/BStackSampleApp.ipa` | BrowserStack's sample iOS app. The sample tests in all three folders use this file. |

## Your original tests

`FrontSystems.MobileAutomation.iOS/` is unchanged. It holds your NUnit tests:

| Test | What it covers |
|---|---|
| `LoginFlowTest` | Onboarding, login and permission dialogs |
| `SaleTest` | Starting a sale and searching for a product |
| `CompleteFlowTest` | Login, cash sale, delete line and cancel sale, park and retrieve, add customer and return, Z-report settlement, logout |

Helpers (`LoginHelper`, `PermissionsHelper`, `BaseIosTest`, `DriverFactory`) and the credentials reader
sit alongside them. The three new folders carry these same flows over, so nothing about your test design
has to change.

## What to expect in each framework folder

Every framework folder follows the same pattern:

1. **A sample test** (`TestCategory=Sample` / `test:sample` / `text-input` flow). It opens BrowserStack's
   sample app, types an email address into a text field and checks it appears in the output label. Use it
   to confirm your BrowserStack account and setup work.
2. **Your tests, translated.** The login, sale, park, return, Z-report and logout flows from your C#
   suite, split into independent tests (Maestro keeps them as one end-to-end flow). They stay skipped
   until you provide your app and test account.
3. **A README** with exact commands for both of the above.
4. **A single config file** for devices, parallel runs and BrowserStack features, so adding devices or
   turning features on is a config edit, not a code change.

| | `appium-nunit/` | `appium-nodejs/` | `maestro/` |
|---|---|---|---|
| Language | C# (same packages and `net10.0` as your project) | JavaScript | YAML flows |
| Runner | NUnit + BrowserStack .NET SDK | Mocha + BrowserStack Node SDK | BrowserStack Maestro runner |
| Config | `browserstack.yml` | `browserstack.yml` | `devices` in the build request |
| Sample test | `Tests/TextInputTest.cs` | `test/specs/text-input.spec.js` | `.maestro/text-input.yaml` |
| Your tests | `Tests/CustomerPosFlowTest.cs` | `test/specs/customer-complete-flow.spec.js` | `.maestro/customer/` |
| Best for | Reusing your C# code as is | JavaScript teams, fastest to adopt | Low-code, quick UI flows |

## Run the BrowserStack sample app (all three frameworks)

You need a BrowserStack account. Your username and access key are under *Account > Settings*.

1. **Set your credentials** in your shell (or in the folder's `.env`, copied from `.env.example`):
   ```bash
   export BROWSERSTACK_USERNAME=<your username>
   export BROWSERSTACK_ACCESS_KEY=<your access key>
   ```
2. **Upload the sample app** from this repo and copy the `app_url` from the response (`bs://...`):
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@sample-app/BStackSampleApp.ipa"
   ```
3. **Use that app in the framework you picked** and run the sample test:

   | Framework | Run |
   |---|---|
   | Appium NUnit | `cd appium-nunit && dotnet tool restore && BS_APP_ID=bs://... dotnet browserstack-sdk --filter "TestCategory=Sample"` |
   | Appium Node.js | `cd appium-nodejs && npm install && BS_APP_ID=bs://... npm run test:sample` |
   | Maestro | see `maestro/README.md` (upload the flows, then start a build with the app) |

The build link is printed at the end of each run and shows video, logs and reports on the BrowserStack
dashboard.

## Run your own app and your own tests

1. **Upload your app** (`.ipa`) the same way, with your file in place of the sample app:
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@/path/to/your-app.ipa"
   ```
2. **Update the app URL.** Set `BS_APP_ID` to the new `bs://...` value. Nothing in the test code changes.
3. **Set your test account** with the variable names your C# suite already reads:
   `TEST_SECONDARY_USERNAME`, `TEST_SECONDARY_PASSWORD`, `FS_UNLOCK_NAME`, `FS_UNLOCK_PIN`.
   Set them in the shell or in the folder's `.env`. Never commit them.
4. **Run your tests:**

   | Framework | Run |
   |---|---|
   | Appium NUnit | `dotnet browserstack-sdk --filter "TestCategory=Customer"` |
   | Appium Node.js | `npm run test:customer` |
   | Maestro | run `.maestro/customer/complete-flow.yaml` (see `maestro/README.md`) |

## Scaling

Devices, parallel threads and features are settings, not code:

- **More devices or OS versions:** add entries under `platforms` in `browserstack.yml` (or to the
  `devices` list in a Maestro build).
- **More parallel runs:** raise `parallelsPerPlatform` in `browserstack.yml`.
- **Reporting, logs and insights:** Test Reporting & Analytics, video, network, device and Appium logs,
  app profiling, self-healing locators and accessibility scans are switches in `browserstack.yml`.

## What has and has not been run

| | Sample app test | Your translated tests |
|---|---|---|
| Appium NUnit | Passed on iPhone 15 and iPhone 16 | Not run against your app (skipped until your account is set) |
| Appium Node.js | Passed on iPhone 15 and iPhone 16 | Not run against your app (skipped until your account is set) |
| Maestro | Passed | Not run against your app |

- **Your locators are untested on your app.** The translated tests reuse the element names from your
  C# suite as they are. Expect a first run to need a few locator fixes.
- **The Maestro sample flow is a first-screen check.** It looks for the Text button, the Alert button and
  the "UI Elements" title. A longer flow that types into the text field failed on the BrowserStack iPhone 16
  (iOS 18.6) because the Text row sits partly under the navigation bar there.
- **The Node.js run logged an accessibility initialisation error**, so the accessibility scan may not
  have run. Percy visual testing is not enabled.
- **Not carried over from your C# suite:** the app-state and lifecycle checks, the stress-test helpers and
  the debug page dumps.
