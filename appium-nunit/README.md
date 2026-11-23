# Appium (C# / NUnit) on BrowserStack

NUnit + Appium (.NET) run on BrowserStack App Automate with the **BrowserStack .NET SDK**
(`browserstack-sdk` dotnet tool). Devices, app, reporting and debug features are configured in
`browserstack.yml`, so scaling to more devices is a config change. The project uses the same
package versions and target framework (`net10.0`) as your existing
`FrontSystems.MobileAutomation.iOS` project, so your tests drop straight in.

```
appium-nunit/
├── BStackAppiumNUnit.csproj
├── browserstack.yml                # devices, app, SDK features
├── .config/dotnet-tools.json       # pins the BrowserStack SDK tool (1.57.0)
├── .env.example                    # copy to .env (git-ignored)
├── Helpers/BaseTest.cs             # session setup + pass/fail reporting to BrowserStack
├── Pages/CustomerPosPage.cs        # your POS app, from the C# suite
├── TestData/                       # test data (no values inside test code)
└── Tests/
    ├── TextInputTest.cs            # sample app test
    └── CustomerPosFlowTest.cs      # your tests, split into independent tests
```

## 1. Try the sample app test (5 minutes)

1. **Install the .NET SDK and the BrowserStack tool:**
   ```bash
   dotnet tool restore
   ```
2. **Set credentials and app** (macOS/Linux shell; use `$env:` on PowerShell). Your BrowserStack
   username and access key are under *Account > Settings*:
   ```bash
   export BROWSERSTACK_USERNAME=...
   export BROWSERSTACK_ACCESS_KEY=...
   ```
3. **Upload the sample app** (the copy kept in this repo at `../sample-app/BStackSampleApp.ipa`) and copy the `app_url` from the response:
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@../sample-app/BStackSampleApp.ipa"
   export BS_APP_ID=bs://abc123...
   export BUILD_NUMBER=local
   ```
4. **Run:**
   ```bash
   dotnet browserstack-sdk --filter "TestCategory=Sample"
   ```
   The test runs in parallel on the devices listed in `browserstack.yml`. The build link is printed at the end of the run.

## 2. Run your own tests

`Tests/CustomerPosFlowTest.cs` and `Pages/CustomerPosPage.cs` carry over your existing flows
(`LoginFlowTest`, `SaleTest`, `CompleteFlowTest` and their helpers) as six independent tests:
cash sale, product search, delete line and cancel sale, park and retrieve, add customer and return,
Z-report settlement and logout. Each test logs in and unlocks first, so none depends on another.

1. **Upload your app** (`.ipa`) with `-F "file=@/path/to/your-app.ipa"` in the upload call above.
2. **Update the app URL:** set `BS_APP_ID` to the new `bs://...` value (no code changes).
3. **Set your test account** with the same variables your suite already uses:
   `TEST_SECONDARY_USERNAME`, `TEST_SECONDARY_PASSWORD`, `FS_UNLOCK_NAME`, `FS_UNLOCK_PIN`. The customer tests are skipped (ignored) until all four are set.
4. **Run:**
   ```bash
   dotnet browserstack-sdk --filter "TestCategory=Customer"
   ```

**Moving more of your tests over:** copy a test class into `Tests/`, inherit from `BaseTest` and
use `Driver` as you do today. The BrowserStack SDK injects the device, OS, app and credentials from
`browserstack.yml`, so you do not set those capabilities in code.

## How to run

Run from this folder and pass the test arguments straight to the SDK, as in the commands above
(`dotnet browserstack-sdk --filter "TestCategory=Sample"`). Do not add `dotnet test` after
`browserstack-sdk`.

**Status:** the sample test (`TestCategory=Sample`) passed on both configured devices (iPhone 15 and
iPhone 16). The customer tests (`TestCategory=Customer`) have **not** been run: they need your app and
test account, and their locators come from your C# suite unchanged, so expect a first run to need small
locator fixes in `CustomerPosPage.cs`.

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
