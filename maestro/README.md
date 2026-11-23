# Maestro on BrowserStack

Maestro flows run on BrowserStack App Automate real devices. There is nothing to install or build:
you upload the app, then upload the flows folder.

```
maestro/
└── .maestro/
    ├── text-input.yaml            # sample app flow
    └── customer/
        ├── login.yaml             # your login + permission dialogs (from the C# suite)
        ├── unlock.yaml            # your lock-screen name + PIN
        └── complete-flow.yaml     # your sale, park, return, Z-report and logout flow
```

## 1. Try the sample app flow (5 minutes)

1. **Upload the sample app** (the copy kept in this repo at `../sample-app/BStackSampleApp.ipa`):
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/upload" \
     -F "file=@../sample-app/BStackSampleApp.ipa"
   ```
   Copy the `app_url` (looks like `bs://abc123...`).
2. **Zip the flows and upload them as the test suite:**
   ```bash
   cd maestro && zip -r flows.zip .maestro
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/maestro/v2/ios/test-suite" \
     -F "file=@flows.zip"
   ```
   Copy the `test_suite_url`.
3. **Run it:**
   ```bash
   curl -u "$BROWSERSTACK_USERNAME:$BROWSERSTACK_ACCESS_KEY" \
     -X POST "https://api-cloud.browserstack.com/app-automate/maestro/v2/ios/build" \
     -H "Content-Type: application/json" \
     -d '{"app":"bs://<app-id>","testSuite":"bs://<test-suite-id>","devices":["iPhone 16-18"],"execute":["text-input"]}'
   ```
   Open the build link from the response to watch the run.

The sample flow, `.maestro/text-input.yaml`, opens the app and checks the first screen
(Text Button, Alert and the "UI Elements" title).

## 2. Run your own flows

`.maestro/customer/` is a translation of the C# suite (`LoginHelper`, `PermissionsHelper`,
`BaseIosTest`, `CompleteFlowTest`): login and iOS permission dialogs, lock-screen unlock, cash sale,
delete line and cancel, park and retrieve, add customer and return, Z-report settlement, logout.

1. **Upload your app** (`.ipa`) with `-F "file=@/path/to/your-app.ipa"` in the upload call above,
   and copy the new `bs://...` value.
2. **Update the app URL:** use the new `bs://` value as `app` in the build call. The flows take your
   app's bundle id through the `APP_ID` variable (`-d '{... "env": {"APP_ID": "com.your.bundle"}}'`).
3. **Pass your test account** as env vars on the same build call: `TEST_SECONDARY_USERNAME`,
   `TEST_SECONDARY_PASSWORD`, `FS_UNLOCK_NAME`, `FS_UNLOCK_PIN`. They are the same names the C# suite reads. Never commit them to the flows.
4. **Run the customer flow only:** set `"execute":["customer/complete-flow"]` in the build call.

**Note:** the element ids are copied from the C# suite and were not run against your app here.
Expect a first run to need a few id tweaks in `.maestro/customer/`.

## Scaling

Add devices in the `devices` array of the build call, for example
`["iPhone 16-18", "iPhone 15-17", "iPad Air 13 2025-18"]`.
