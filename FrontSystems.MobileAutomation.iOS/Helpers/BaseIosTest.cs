using System.Text.RegularExpressions;
using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace FrontSystems.MobileAutomation.iOS.Helpers;

public class BaseIosTest : BaseTest
{
    /// <summary>
    /// Display name shown on the lock screen for fiscal test users.
    /// All fiscal test accounts (Sweden, Dubai, USMA) are registered as "Fiscal Auto Test".
    /// </summary>
    protected const string FiscalDisplayName = "Fiscal Auto Test";
    protected const string FiscalUserPin = "1234";

    /// <summary>
    /// Queries the iOS app state via the XCUITest `mobile: queryAppState` command (a POST/execute,
    /// so it is unaffected by the .NET 10 GET-command deserialization bug). Returns the raw state:
    /// 0=NotInstalled, 1=NotRunning, 2=RunningBackgroundSuspended, 3=RunningBackground,
    /// 4=RunningForeground. Returns -1 if the query itself fails.
    /// </summary>
    protected long QueryAppState()
    {
        try
        {
            var result = Driver.ExecuteScript(
                "mobile: queryAppState",
                new Dictionary<string, object> { { "bundleId", DriverFactory.AppBundleId } });
            return Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"queryAppState failed (cannot determine app state): {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Fails the test if the app under test has crashed/terminated (state NotInstalled or NotRunning)
    /// after the named step. By default an *unknown* state (-1, the query itself failed) does NOT fail —
    /// so this is safe to sprinkle through the happy-path flow without false positives. Set
    /// <paramref name="failIfStateUnknown"/> = true in a crash-regression sentinel: a hard crash can kill
    /// the session so that queryAppState itself fails (state -1), and there you want that to fail fast and
    /// loud rather than mask the crash. The BrowserStack crash/device logs (surfaced in TearDown) carry
    /// the native stack.
    /// </summary>
    protected void AssertAppAlive(string step, bool failIfStateUnknown = false)
    {
        var state = QueryAppState();
        if (state == 0 || state == 1)
        {
            Assert.Fail(
                $"APP CRASHED after step '{step}': app state = {state} " +
                "(0=NotInstalled, 1=NotRunning). Check the BrowserStack crash/device logs for the native stack.");
        }

        if (state == -1 && failIfStateUnknown)
        {
            Assert.Fail(
                $"APP STATE UNKNOWN after step '{step}': queryAppState could not be executed — likely a hard " +
                "crash / dead session. Check the BrowserStack crash/device logs for the native stack.");
        }

        if (state == -1)
        {
            Console.WriteLine($"App state UNKNOWN after '{step}' (queryAppState failed — not treated as a crash).");
        }
        else
        {
            Console.WriteLine($"App alive after '{step}' (state {state}).");
        }
    }

    /// <summary>
    /// Exercises the iOS lifecycle/GC paths that our crash families hit but the happy-path sale flow
    /// does not: rotation (UITraitChangeRegistry / UITrait change family) and background→foreground
    /// (handler-disconnect / GCSweepView family). Asserts the app is alive after each action, then
    /// restores portrait. Use in a dedicated crash-regression test.
    /// </summary>
    protected void StressLifecycle(int cycles = 4, bool failIfStateUnknown = false)
    {
        for (int i = 0; i < cycles; i++)
        {
            try { Driver.Orientation = ScreenOrientation.Landscape; } catch (Exception ex) { Console.WriteLine($"rotate landscape skipped: {ex.Message}"); }
            AssertAppAlive($"rotate landscape (cycle {i})", failIfStateUnknown);
            Thread.Sleep(500);

            try { Driver.Orientation = ScreenOrientation.Portrait; } catch (Exception ex) { Console.WriteLine($"rotate portrait skipped: {ex.Message}"); }
            AssertAppAlive($"rotate portrait (cycle {i})", failIfStateUnknown);
            Thread.Sleep(500);

            try
            {
                Driver.ExecuteScript("mobile: backgroundApp", new Dictionary<string, object> { { "seconds", 1 } });
            }
            catch (Exception ex) { Console.WriteLine($"backgroundApp skipped: {ex.Message}"); }
            AssertAppAlive($"background to foreground (cycle {i})", failIfStateUnknown);
            Thread.Sleep(500);
        }

        try { Driver.Orientation = ScreenOrientation.Portrait; } catch (Exception ex) { Console.WriteLine($"final portrait restore skipped: {ex.Message}"); }
    }

    /// <summary>
    /// France NF525: the last daily closure being 24h+ old blocks further sales until a
    /// Z-report is run. Must be called BEFORE CheckAndDismissOkPopup() (and before any other
    /// generic "OK"/"Done"/"Close" fallback tap, e.g. DismissReceiptViews' name-search fallback)
    /// — those handlers match by accessibility id/name alone with no dialog-specific scoping,
    /// so they silently swallow this alert's OK button first. Once dismissed, the "Return to
    /// inventory" tag on the return line still matches the generic recovery loop in
    /// WaitForPOSReady, which then retries BtnReturnReasonDone for the full timeout with no
    /// visible cause. Fail fast with a clear signal instead (confirmed expected by Vibeke
    /// Glosimot, 2026-09-17 — a real fiscal gate, not something to code around here).
    ///
    /// Matches the blocking modal's longer message text, not the short "Z-report needed"
    /// substring alone — that substring is also the exact text of the harmless, non-blocking
    /// Sale_START toast (ToastService.SetSigningserviceNeedZReport, ToastService.cs:553), which
    /// VerifyOverdueClosureWarning deliberately expects to see. Matching on it here would throw
    /// on every overdue-but-otherwise-normal sale, not just the real blocking gate (caught by
    /// Copilot review on this PR, 2026-09-17).
    /// </summary>
    protected void CheckAndFailOnZReportBlock()
    {
        // Zero-timeout lookup, not Driver.FindElement: this runs on every WaitForPOSReady
        // iteration, DismissReceiptViews call and TryReturnToMainMenu call, so waiting out
        // the driver's default 10s implicit wait every time the (normal-case) alert is absent
        // would add ~10s to every one of those and risk blowing outer scenario timeouts
        // (caught by Copilot review on this PR, 2026-09-17).
        var zReportAlert = FindElementWithTimeout(By.XPath(
            "//XCUIElementTypeStaticText[contains(@name, 'run a Z-report before completing this sale')]"), 0);
        if (zReportAlert?.Displayed == true)
        {
            TakeScreenshot("ZReportNeeded_Blocking");
            throw new InvalidOperationException(
                "Z-report needed: the last daily closure is 24h+ old and France fiscal rules " +
                "block further sales until a Z-report is run. Run a Settlement Z-Report on " +
                "this POS manually, then retry the test.");
        }
    }

    /// <summary>
    /// Locator for an "OK" button. The app's popups set AutomationIds (BtnConfirmOkButton, BtnAlertOkButton,
    /// BtnDialogOkClicked), which iOS exposes as the Appium <c>name</c>, so the visible text is matched
    /// through <c>label</c> and native alerts through <c>name</c>.
    /// </summary>
    protected static readonly By OkButtonLocator = By.XPath(
        "//XCUIElementTypeButton[@label='OK' or @name='OK' or @name='BtnConfirmOkButton' or @name='BtnAlertOkButton' or @name='BtnDialogOkClicked']");

    /// <summary>
    /// Dismisses an "OK" popup if present (e.g. info alerts, login confirmations).
    /// Safe to call even when no popup is shown.
    /// </summary>
    protected void CheckAndDismissOkPopup()
    {
        // "OK" — generic confirm popup
        try
        {
            var okBtn = Driver.FindElement(OkButtonLocator);
            if (okBtn?.Displayed == true) { okBtn.Click(); Thread.Sleep(500); Console.WriteLine("✅ OK popup dismissed."); return; }
        }
        catch { }

        // "Keep" — debug-settings popup that appears for viglo@eg.no on dev builds
        try
        {
            var keepBtn = Driver.FindElement(MobileBy.AccessibilityId("Keep"));
            if (keepBtn?.Displayed == true) { keepBtn.Click(); Thread.Sleep(500); Console.WriteLine("✅ Debug-settings popup dismissed (Keep)."); }
        }
        catch { }
    }

    /// <summary>
    /// If the PIN keypad is showing, enters the given PIN. No-op otherwise.
    /// Requires BOTH button '1' AND button '2' to be present — lock-screen
    /// notification counters are StaticTexts (not Buttons), so a lone StaticText '1'
    /// no longer causes a false-positive detection.
    /// </summary>
    protected void EnterPinIfRequired(string pin)
    {
        // Wait up to 8s for the PIN pad's '1' button — page transition from
        // UsersPage to PINPage can take several seconds on a loaded device.
        // Never fall back to StaticText: notification counters on the lock screen
        // are StaticTexts named '1' and produce false positives.
        var pinBtn1 = FindElementWithTimeout(
            By.XPath("//XCUIElementTypeButton[@label='1' or @name='1']"), 8);

        // Confirm this is really a PIN pad by verifying button '2' also exists.
        // A lone button named '1' elsewhere (e.g. a custom control) won't pass this check.
        var pinBtn2 = pinBtn1?.Displayed == true
            ? FindElementWithTimeout(By.XPath("//XCUIElementTypeButton[@label='2' or @name='2']"), 2)
            : null;

        if (pinBtn1?.Displayed != true || pinBtn2?.Displayed != true)
        {
            Console.WriteLine("No PIN screen detected — skipping PIN entry.");
            return;
        }

        Console.WriteLine($"PIN screen detected — entering PIN: {pin}");
        foreach (char digit in pin)
        {
            var btn = FindElementWithTimeout(
                By.XPath($"//XCUIElementTypeButton[@label='{digit}' or @name='{digit}']"), 3);
            if (btn?.Displayed == true)
            {
                btn.Click();
                Thread.Sleep(300);
            }
            else
            {
                Console.WriteLine($"⚠️ PIN button '{digit}' not found.");
            }
        }
        TakeScreenshot("PinEntered");
        Console.WriteLine("✅ PIN entered.");
    }

    /// <summary>
    /// Tries to tap the user row on the lock screen / UsersPage and enter the PIN.
    /// Tries multiple strategies because the lock screen shows the display name
    /// (e.g. "Fiscal Auto Test"), not the email used as the login username.
    /// Returns true if a user row was found and tapped.
    /// </summary>
    protected bool TryUnlockAsUser(string displayName, string pin)
    {
        // Prefer the row wrapping the name over the bare StaticText. UsersPage.xaml's row
        // template (line 264) is a Grid with a TapGestureRecognizer, not a native Cell/Button,
        // which iOS typically exposes as XCUIElementTypeOther — try that ancestor first (per
        // Copilot review on this PR, 2026-09-17), then Cell/Button in case a differently
        // rendered screen uses those instead, then the bare label as a last resort. The label
        // tap alone was empirically confirmed working for the France account once the
        // displayName search actually matched (2026-09-17 full runs) — before that fix the
        // search for the wrong name fell through to a much broader "first tappable label
        // anywhere" fallback that likely hit an unrelated element, not this row at all.
        var userText = FindElementWithTimeout(
            By.XPath($"//XCUIElementTypeStaticText[@name='{displayName}']/ancestor::XCUIElementTypeOther[1]"), 1)
                     ?? FindElementWithTimeout(
            By.XPath($"//XCUIElementTypeStaticText[@name='{displayName}']/ancestor::XCUIElementTypeCell[1]"), 1)
                     ?? FindElementWithTimeout(
            By.XPath($"//XCUIElementTypeStaticText[@name='{displayName}']/ancestor::XCUIElementTypeButton[1]"), 1)
                     ?? FindElementWithTimeout(
            By.XPath($"//XCUIElementTypeStaticText[@name='{displayName}']"), 2);

        if (userText?.Displayed != true)
        {
            userText = FindElementWithTimeout(
                By.XPath($"//XCUIElementTypeCell[.//XCUIElementTypeStaticText[contains(@name,'{displayName}')]]"), 2);
        }

        if (userText?.Displayed != true)
        {
            // Lock screen visible? Tap the first visible user name StaticText (iOS lobby uses Buttons,
            // not Cells — "(//XCUIElementTypeCell)[1]" does not match).
            var lockHint = FindElementWithTimeout(
                By.XPath("//XCUIElementTypeStaticText[contains(@name,'Press your name to unlock')]"), 1);
            if (lockHint?.Displayed == true)
            {
                // Try first cell (Android / older iOS), then first user name StaticText inside a Button
                userText = FindElementWithTimeout(By.XPath("(//XCUIElementTypeCell)[1]"), 1)
                        ?? FindElementWithTimeout(
                               By.XPath("(//XCUIElementTypeButton//XCUIElementTypeStaticText[string-length(@name)>1])[1]"), 1);
            }
        }

        if (userText?.Displayed != true) return false;

        Console.WriteLine($"Tapping user '{displayName}' to unlock...");
        userText.Click();

        // Wait for PINPage to load (button '1' appears) rather than sleeping a fixed time.
        // PINPage navigation can take 1-5s on a loaded device; 6s ceiling is generous but safe.
        // If '1' never appears, POS may not require a PIN — EnterPinIfRequired will be a no-op.
        var pinPageDeadline = DateTime.Now.AddSeconds(6);
        while (DateTime.Now < pinPageDeadline)
        {
            var probe = FindElementWithTimeout(By.XPath("//XCUIElementTypeButton[@label='1' or @name='1']"), 0);
            if (probe?.Displayed == true) break;
            if (IsPosReady()) return true; // no PIN required — already unlocked
            Thread.Sleep(400);
        }

        EnterPinIfRequired(pin);
        Thread.Sleep(2000);
        return true;
    }

    /// <summary>
    /// Waits until POS is ready (MenuStartSale visible), handling lock screen,
    /// system alerts, OK popups, and the Return-to-inventory reason screen along the way.
    /// </summary>
    protected void WaitForPOSReady(int timeoutSeconds, string displayName = FiscalDisplayName, string pin = FiscalUserPin)
    {
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(Driver, TimeSpan.Zero);
        try
        {
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            while (DateTime.Now < deadline)
            {
                if (IsPosReady()) { Console.WriteLine("✅ POS ready."); return; }

                CheckAndFailOnZReportBlock();

                DismissSystemAlert();
                CheckAndDismissOkPopup();

                try
                {
                    var returnTitle = Driver.FindElement(By.XPath("//XCUIElementTypeStaticText[@name='Return to inventory']"));
                    if (returnTitle.Displayed)
                    {
                        Console.WriteLine("Return reason screen detected — tapping Done...");
                        try
                        {
                            var doneBtn = Driver.FindElement(MobileBy.AccessibilityId("BtnReturnReasonDone"));
                            if (doneBtn.Displayed) doneBtn.Click();
                        }
                        catch { }
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                catch { }

                // Recovery: if the app is stuck on an open sale screen, cancel it so POS returns home
                var moreBtn = FindElementWithTimeout(MobileBy.AccessibilityId("btnMore"), 0);
                if (moreBtn?.Displayed == true)
                {
                    Console.WriteLine("Sale screen detected — tapping More to cancel sale...");
                    moreBtn.Click();
                    Thread.Sleep(1000);
                    var cancelBtn = FindElementWithTimeout(MobileBy.AccessibilityId("BtnCancelSale"), 2)
                        ?? FindElementWithTimeout(By.XPath("//*[@name='Cancel sale']"), 2);
                    if (cancelBtn?.Displayed == true)
                    {
                        cancelBtn.Click();
                        Console.WriteLine("Cancel sale tapped — waiting for POS home...");
                        Thread.Sleep(2000);
                    }
                    continue;
                }

                if (TryUnlockAsUser(displayName, pin))
                {
                    Thread.Sleep(2000);
                    continue;
                }

                Thread.Sleep(500);
            }
        }
        finally
        {
            DriverFactory.SetImplicitWait(Driver, originalImplicitWait);
        }

        TakeScreenshot("ONBOARDING_ERROR_Timeout");
        DumpVisibleStaticText();
        throw new InvalidOperationException($"POS-ready wait timed out after {timeoutSeconds}s — MenuStartSale never appeared. Check screenshot ONBOARDING_ERROR_Timeout.");
    }

    /// <summary>
    /// Quick check: if MenuStartSale is visible, return immediately.
    /// Otherwise run the full WaitForPOSReady unlock loop.
    /// </summary>
    protected void EnsureUnlocked(string? displayName = null, string? pin = null)
    {
        if (IsPosReady()) return;
        // Defaults can be overridden per machine/POS, e.g. FS_UNLOCK_NAME=Vibeke on POS 467 (locks after ~30 s idle).
        displayName ??= Environment.GetEnvironmentVariable("FS_UNLOCK_NAME") ?? FiscalDisplayName;
        pin ??= Environment.GetEnvironmentVariable("FS_UNLOCK_PIN") ?? FiscalUserPin;
        Console.WriteLine("POS locked — unlocking...");
        WaitForPOSReady(90, displayName, pin);
    }

    /// <summary>
    /// True when the app is already provisioned and sitting on the POS home or the user lock screen
    /// ("Press your name to unlock"), i.e. account login/onboarding must NOT run (LoginHelper.PerformLogin
    /// throws there: no onboarding controls and no login fields). Polls up to timeoutSeconds because the
    /// app may still be launching; a fresh install never shows either marker, so it returns false.
    /// </summary>
    protected bool IsProvisionedLockOrHome(int timeoutSeconds = 8)
    {
        var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
        do
        {
            if (IsPosReady()) return true;
            var lockHint = FindElementWithTimeout(
                By.XPath("//XCUIElementTypeStaticText[contains(@name,'Press your name to unlock')]"), 1);
            if (lockHint?.Displayed == true) return true;
        } while (DateTime.Now < deadline);
        return false;
    }

    // iOS 26: MAUI Other elements (AutomationId="MenuStartSale") may not propagate to XCUITest
    // accessibilityIdentifier. Fall back to StaticText "New sale" which is always visible when ready.
    private bool IsPosReady()
    {
        var el = FindElementWithTimeout(MobileBy.AccessibilityId("MenuStartSale"), 1)
              ?? FindElementWithTimeout(By.XPath("//XCUIElementTypeStaticText[@name='New sale']"), 1);
        return el?.Displayed == true;
    }

    private void DumpVisibleStaticText()
    {
        try
        {
            var els = Driver.FindElements(By.XPath("//XCUIElementTypeStaticText"));
            Console.WriteLine($"Screen when timed out ({els.Count} text elements):");
            foreach (var el in els.Take(15))
                Console.WriteLine($"  - '{el.Text}' / name='{el.GetAttribute("name")}'");
        }
        catch { }
    }

    /// <summary>
    /// Dismisses iOS system alert popups (e.g. LAN access, camera, notifications).
    /// Uses SwitchTo().Alert().Accept() since system dialogs are not part of the app's view hierarchy.
    /// Safe to call even if no alert is present.
    /// </summary>
    protected void DismissSystemAlert()
    {
        try
        {
            Driver.SwitchTo().Alert().Accept();
            Thread.Sleep(500);
            Console.WriteLine("✅ System alert dismissed.");
        }
        catch
        {
            // No alert present — safe to ignore
        }
    }

    protected void TryHideKeyboard()
    {
        try
        {
            var returnKey = Driver.FindElement(By.Name("return"));
            if (returnKey.Displayed)
            {
                returnKey.Click();
                Thread.Sleep(300);
                return;
            }
        }
        catch { }

        foreach (var key in new[] { "Return", "Done", "Search" })
        {
            try
            {
                var el = Driver.FindElement(By.Name(key));
                if (el.Displayed)
                {
                    el.Click();
                    Thread.Sleep(300);
                    return;
                }
            }
            catch { }
        }
    }

    protected AppiumElement? WaitForSecureTextField(int timeoutSeconds)
    {
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(Driver, TimeSpan.Zero);
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            return wait.Until(_ =>
            {
                try
                {
                    var secureFields = Driver.FindElements(By.XPath("//XCUIElementTypeSecureTextField"));
                    foreach (var field in secureFields)
                    {
                        if (field.Displayed && field.Enabled)
                            return field;
                    }
                    return null;
                }
                catch (NoSuchElementException) { return null; }
            });
        }
        catch (WebDriverTimeoutException) { return null; }
        finally
        {
            DriverFactory.SetImplicitWait(Driver, originalImplicitWait);
        }
    }

    protected int GetTotalSalesAmount()
    {
        try
        {
            var label = Driver.FindElement(MobileBy.AccessibilityId("ExpectedCashAmount"));
            var text = label?.Text ?? "";
            if (string.IsNullOrEmpty(text)) return 0;

            var cleaned = text.Replace(" ", "").Trim();
            if (cleaned.Contains(',') && !cleaned.Contains('.'))
                cleaned = cleaned.Split(',')[0];
            else if (cleaned.Contains('.') && cleaned.Contains(','))
                cleaned = cleaned.Replace(",", "").Split('.')[0];
            else if (cleaned.Contains('.'))
                cleaned = cleaned.Split('.')[0];

            // Confirmed 2026-09-03 (France, low test-transaction volume — "Grand total cash
            // EUR 16.00"): the >= 100 floor below has no documented rationale and rejects a
            // genuinely-read, valid small total, which then submits a withdrawal of 0 against
            // a real 16.00 balance and trips a downstream amount-mismatch warning. A
            // successful int.TryParse is already the real signal that this is a number and
            // not placeholder text; only the upper sanity ceiling is worth keeping.
            if (int.TryParse(cleaned, out int amount) && amount >= 0 && amount <= 100000)
            {
                Console.WriteLine($"Found grand total: {text} (parsed as {amount})");
                return amount;
            }
        }
        catch { }

        Console.WriteLine("❌ Could not read grand total from Z-Report");
        return 0;
    }

    protected AppiumElement? FindSettlementButton()
    {
        try
        {
            var button = Driver.FindElement(MobileBy.AccessibilityId("BtnSettlement"));
            return button?.Displayed == true ? button : null;
        }
        catch { return null; }
    }

    protected AppiumElement? FindYesButton()
    {
        foreach (var sel in new[] { "BtnConfirmOkButton", "Yes", "OK", "BtnYes", "Confirm" })
        {
            try
            {
                var el = Driver.FindElement(MobileBy.AccessibilityId(sel));
                if (el?.Displayed == true) return el;
            }
            catch { }
        }
        return null;
    }

    protected AppiumElement? FindWithdrawalButton()
    {
        try
        {
            var button = Driver.FindElement(MobileBy.AccessibilityId("BtnAddWithdrawal"));
            return button?.Displayed == true ? button : null;
        }
        catch { return null; }
    }

    protected AppiumElement? FindCashInPOSField()
    {
        try
        {
            var field = Driver.FindElement(MobileBy.AccessibilityId("CashHoldings"));
            if (field?.Displayed == true) return field;
        }
        catch { }

        foreach (var sel in new[] { "Cash in POS", "txtCashInPOS", "EntryCashInPOS" })
        {
            try
            {
                var el = Driver.FindElement(MobileBy.AccessibilityId(sel));
                if (el?.Displayed == true) return el;
            }
            catch { }
        }
        return null;
    }

    protected AppiumElement? FindAmountField()
    {
        try
        {
            var field = Driver.FindElement(MobileBy.AccessibilityId("Amount"));
            if (field?.Displayed == true && field.Enabled) return field;
        }
        catch { }

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var textFields = Driver.FindElements(By.XPath("//XCUIElementTypeTextField | //android.widget.EditText"));
                foreach (var field in textFields)
                {
                    if (field.Displayed && field.Enabled) return field;
                }
            }
            catch { }
            if (attempt < 2) Thread.Sleep(300);
        }
        return null;
    }

    protected void WaitForLoginScreenToAppear(int timeoutSeconds)
    {
        // Mirrors LoginHelper.WaitForLoginScreenToAppear. The fiscal/performance onboarding
        // methods raced straight into a field lookup with no pre-check that the login form
        // had actually rendered — see FRO-16704 ("Username field not found", chronic).
        // Implicit wait must be zeroed first — FindElements otherwise blocks up to the implicit
        // wait on every poll, making this take far longer than timeoutSeconds (same pattern as
        // WaitForSecureTextField below).
        var originalImplicitWait = DriverFactory.CurrentImplicitWait;
        DriverFactory.SetImplicitWait(Driver, TimeSpan.Zero);
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(_ =>
            {
                try
                {
                    return Driver.FindElements(By.XPath(
                        "//XCUIElementTypeTextField | //XCUIElementTypeSecureTextField | //android.widget.EditText")).Count > 0;
                }
                catch { return false; }
            });
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("⚠ Login screen never appeared — will attempt to find field anyway");
        }
        finally
        {
            DriverFactory.SetImplicitWait(Driver, originalImplicitWait);
        }
    }

    protected AppiumElement? FindUsernameFieldRobust(int perLocatorTimeoutSeconds = 2)
    {
        // Mirrors LoginHelper.FindUsernameField's multi-locator fallback. The fiscal/performance
        // onboarding methods used only MobileBy.AccessibilityId("AutUserName") — exactly the
        // single locator FRO-16588 found unreliable on iOS 26 for CompleteFlowTest — see FRO-16704.
        By[] locators =
        [
            MobileBy.AccessibilityId("AutUserName"),
            By.XPath("//XCUIElementTypeTextField[@identifier='AutUserName' or @name='AutUserName']"),
            By.XPath("//XCUIElementTypeTextField[contains(@name, 'User')]"),
            By.XPath("//XCUIElementTypeTextField[contains(@placeholder, 'user') or contains(@placeholder, 'email') or contains(@placeholder, 'User') or contains(@placeholder, 'Email')]"),
            By.XPath("//android.widget.EditText[@resource-id='com.eg.dk.frontsystems:id/AutUserName']"),
            By.XPath("//android.widget.EditText[contains(@hint, 'user') or contains(@hint, 'email') or contains(@hint, 'User') or contains(@hint, 'Email')]"),
            By.XPath("//XCUIElementTypeTextField[1]"),
            By.XPath("//android.widget.EditText[1]"),
        ];

        foreach (var locator in locators)
        {
            var field = FindElementWithTimeout(locator, perLocatorTimeoutSeconds);
            if (field != null) return field;
        }
        return null;
    }

    protected void DumpLoginScreenDiagnostics()
    {
        // Called right before throwing "Username field not found" in the fiscal/performance
        // onboarding methods (FRO-16704). The BrowserStack screenshot at that failure point
        // showed a fully rendered screen ("You must log in as a user with permissions to
        // install POS") with the username field pre-filled — likely by iOS Keychain AutoFill,
        // not our own SendKeys — meaning it's probably a different screen/element set than the
        // standard onboarding login form, using different accessibility identifiers. This dumps
        // the actual element tree so the real identifiers can be added to
        // FindUsernameFieldRobust instead of guessing again.
        try
        {
            Console.WriteLine("\n=== LOGIN DIAGNOSTICS: all text fields on screen ===");
            var textFields = Driver.FindElements(By.XPath(
                "//XCUIElementTypeTextField | //XCUIElementTypeSecureTextField | //android.widget.EditText"));
            Console.WriteLine($"Text fields found: {textFields.Count}");
            foreach (var field in textFields)
            {
                try
                {
                    // Redact 'value' — on a login screen this can be a real username/password
                    // (e.g. iOS Keychain AutoFill), not something safe to print to CI logs.
                    var value = field.GetAttribute("value");
                    var valueRedacted = string.IsNullOrEmpty(value) ? "(empty)" : $"[REDACTED, {value.Length} chars]";
                    Console.WriteLine($"  name='{field.GetAttribute("name")}' label='{field.GetAttribute("label")}' " +
                        $"identifier='{field.GetAttribute("identifier")}' value='{valueRedacted}' " +
                        $"placeholder='{field.GetAttribute("placeholder")}'");
                }
                catch (Exception ex) { Console.WriteLine($"  (error reading field: {ex.Message})"); }
            }

            Console.WriteLine("\n=== LOGIN DIAGNOSTICS: all static text on screen ===");
            var staticTexts = Driver.FindElements(By.XPath("//XCUIElementTypeStaticText"));
            foreach (var el in staticTexts.Take(20))
            {
                try { Console.WriteLine($"  '{el.GetAttribute("name") ?? el.GetAttribute("label")}'"); }
                catch { }
            }

            // Redact value="..." attributes before printing — PageSource embeds the same
            // field values (username/password) that the loop above redacts individually.
            var pageSource = System.Text.RegularExpressions.Regex.Replace(
                Driver.PageSource, "value=\"[^\"]*\"", "value=\"[REDACTED]\"");
            Console.WriteLine("\n=== LOGIN DIAGNOSTICS: PageSource (first 4000 chars, values redacted) ===");
            Console.WriteLine(pageSource.Substring(0, Math.Min(4000, pageSource.Length)));
            Console.WriteLine("=== END LOGIN DIAGNOSTICS ===\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dump login screen diagnostics: {ex.Message}");
        }
    }

    protected AppiumElement? FindDoneButton()
    {
        // The Payment/Discount/PriceOverride Done buttons now carry an AutomationId, which
        // iOS reports as the element name instead of the "Done" label — look for those first.
        foreach (var sel in new[] { "BtnPaymentDone", "BtnDiscountDone", "BtnPriceOverrideDone", "BtnReturnReasonDone", "BtnWithdrawalWithdraw", "Done", "OK", "BtnDone" })
        {
            try
            {
                var el = Driver.FindElement(MobileBy.AccessibilityId(sel));
                if (el?.Displayed == true) return el;
            }
            catch { }
        }
        return null;
    }

    protected AppiumElement? FindNoReceiptButton()
    {
        foreach (var sel in new[] { "BtnNoReceipt", "menuNoReceipt", "No Receipt", "No receipt", "Skip" })
        {
            try
            {
                var el = Driver.FindElement(MobileBy.AccessibilityId(sel));
                if (el?.Displayed == true) return el;
            }
            catch { }
        }
        return null;
    }

    protected AppiumElement? FindPrintReceiptButton()
    {
        foreach (var sel in new[] { "BtnReceipt", "menuReceipt", "BtnPrintReceipt", "menuPrintReceipt", "Print Receipt", "Print receipt" })
        {
            try
            {
                var el = Driver.FindElement(MobileBy.AccessibilityId(sel));
                if (el?.Displayed == true) return el;
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// Finishing a return opens a "Sale" popup (Cash / Voucher / Bank transfer / More) for the payout. The "Cash" item
    /// has no AutomationId (only its label), and the bottom-bar menuCash is hidden behind the popup, so look for the
    /// label. Returns true when the payout popup was shown and Cash was tapped.
    /// </summary>
    protected bool GiveCashBackIfPrompted(int timeoutSeconds = 5)
    {
        var cash = FindElementWithTimeout(MobileBy.AccessibilityId("Cash"), timeoutSeconds)
                ?? FindElementWithTimeout(By.Name("Cash"), 2);
        if (cash == null || !cash.Displayed) return false;
        cash.Click();
        Thread.Sleep(1000);
        return true;
    }

    /// <summary>
    /// Leaves the receipt screen after payment. Prefers "No receipt" (no printer needed, needs the POS setting
    /// ShowNoReceipt for cash sales) and only prints when the Print button is enabled, so a test POS without a printer
    /// does not hang on the spinner. Waits for the home screen before returning. Returns true when "No receipt" was used.
    /// </summary>
    protected bool FinishReceiptScreen(int homeTimeoutSeconds = 15)
    {
        var usedNoReceipt = false;
        var noReceiptButton = FindElementWithTimeout(MobileBy.AccessibilityId("BtnNoReceipt"), 5); // BtnNoReceipt is the receipt-page id; legacy selectors are not probed (each miss costs the full implicit wait)
        if (noReceiptButton != null)
        {
            noReceiptButton.Click();
            usedNoReceipt = true;
        }
        else
        {
            var printReceiptButton = FindPrintReceiptButton();
            if (printReceiptButton == null)
                throw new InvalidOperationException("Neither No receipt nor Print receipt found on the receipt screen");
            if (!printReceiptButton.Enabled)
                throw new InvalidOperationException(
                    "Print receipt is disabled (no printer) and No receipt is hidden - enable the POS setting ShowNoReceipt");
            printReceiptButton.Click();
        }

        if (FindElementWithTimeout(MobileBy.AccessibilityId("MenuStartSale"), homeTimeoutSeconds) == null)
            throw new InvalidOperationException("Home screen (MenuStartSale) not shown after finishing the receipt screen");
        return usedNoReceipt;
    }

    protected void PerformSingleSale(int saleNumber)
    {
        Console.WriteLine($"Sale {saleNumber} - Ensuring POS is ready before starting sale...");
        EnsureUnlocked();

        // Dismiss any system alert that appeared after POS was deemed ready (e.g. Local Network Access popup).
        DismissSystemAlert();
        CheckAndDismissOkPopup();

        Console.WriteLine($"Sale {saleNumber} - Clicking MenuStartSale...");
        var startSaleButton = FindElementWithTimeout(MobileBy.AccessibilityId("MenuStartSale"), 5);
        if (startSaleButton == null)
        {
            TakeScreenshot($"ERROR_Sale{saleNumber}_NoMenuStartSale");
            throw new InvalidOperationException($"MenuStartSale button not found for sale {saleNumber}");
        }

        startSaleButton.Click();
        Thread.Sleep(100);

        Console.WriteLine($"Sale {saleNumber} - Looking for barcode field...");
        var barcodeField = FindElementWithTimeout(MobileBy.AccessibilityId("ScanCode"), 5);
        if (barcodeField == null)
        {
            TakeScreenshot($"ERROR_Sale{saleNumber}_NoScanCode");
            throw new InvalidOperationException($"ScanCode field not found for sale {saleNumber}");
        }

        barcodeField.Click();
        barcodeField.Clear();
        barcodeField.SendKeys("5401157473672");
        TryHideKeyboard();
        Thread.Sleep(1000);

        Console.WriteLine($"Sale {saleNumber} - Looking for Cash button...");
        var cashButton = FindElementWithTimeout(MobileBy.AccessibilityId("menuCash"), 3);
        if (cashButton == null)
        {
            TakeScreenshot($"ERROR_Sale{saleNumber}_NoCashButton");
            throw new InvalidOperationException($"Cash button not found for sale {saleNumber}");
        }

        cashButton.Click();
        Thread.Sleep(100);
        TapCenterScreen();
        Thread.Sleep(100);

        Console.WriteLine($"Sale {saleNumber} - Looking for amount field...");
        var amountField = FindAmountField();
        if (amountField == null)
        {
            TakeScreenshot($"ERROR_Sale{saleNumber}_NoAmountField");
            throw new InvalidOperationException($"Amount field not found for sale {saleNumber}");
        }

        amountField.Click();
        amountField.Clear();
        amountField.SendKeys("299");
        Thread.Sleep(100);

        var doneButton = FindDoneButton();
        if (doneButton == null)
        {
            TakeScreenshot($"ERROR_Sale{saleNumber}_NoDoneButton");
            throw new InvalidOperationException($"Done button not found for sale {saleNumber}");
        }

        doneButton.Click();
        Thread.Sleep(3000);

        var noReceipt = FinishReceiptScreen();
        Console.WriteLine($"✅ Sale {saleNumber} completed{(noReceipt ? "" : " with receipt printed")}!");
    }

    protected void PerformZReportSettlement()
    {
        // Wait for the POS home screen to be fully rendered before looking for the More button.
        // Without this, a transition from the last sale can leave the screen mid-animation and
        // the More button is not yet in the XCUITest hierarchy (observed on iPad Pro 11 2021).
        var homeScreen = FindElementWithTimeout(MobileBy.AccessibilityId("MenuStartSale"), 10);
        if (homeScreen == null)
            Console.WriteLine("⚠ MenuStartSale not visible before Z-Report — proceeding anyway");

        // Primary: By.Name matches the visible label directly and is instant.
        // AccessibilityId("More") and ("btnMore") are fallbacks for devices where the label
        // does not propagate to XCUITest (observed on iPad Pro 11 2021 and iPad Air 5).
        var moreButton = FindElementWithTimeout(By.Name("More"), 5)
            ?? FindElementWithTimeout(MobileBy.AccessibilityId("More"), 3)
            ?? FindElementWithTimeout(MobileBy.AccessibilityId("btnMore"), 3);
        if (moreButton == null)
        {
            TakeScreenshot("ERROR_NoMoreButton");
            throw new InvalidOperationException("More button not found — cannot start Z-Report settlement");
        }

        moreButton.Click();
        TakeScreenshot("03_MoreClicked");
        Thread.Sleep(1000);

        // Primary: search by visible label — the test environment uses English, so "Z Report" is reliable.
        // SettlementZReportButton AccessibilityId is set in the ViewModel but may not propagate fast
        // enough — using it as secondary to avoid BrowserStack self-healing clicking the wrong element.
        AppiumElement? zReportOption = null;
        foreach (var name in new[] { "Z Report", "Z-Report", "Settlement Z-Report" })
        {
            try
            {
                zReportOption = Driver.FindElement(By.Name(name));
                if (zReportOption?.Displayed == true) break;
                zReportOption = null;
            }
            catch { }
        }
        if (zReportOption == null)
        {
            foreach (var sel in new[] { "SettlementZReportButton", "Settlement Z-Report", "ZReport", "BtnZReport", "MenuZReport", "Z-Report", "Z Report" })
            {
                try
                {
                    zReportOption = Driver.FindElement(MobileBy.AccessibilityId(sel));
                    if (zReportOption?.Displayed == true) break;
                    zReportOption = null;
                }
                catch { }
            }
        }

        if (zReportOption == null)
        {
            TakeScreenshot("ERROR_NoZReport");
            throw new InvalidOperationException("Z-Report option not found in popup");
        }

        zReportOption.Click();
        TakeScreenshot("04_ZReportClicked");
        Thread.Sleep(2000);

        // "Initializing settlement" spinner can stay up for several seconds on slower POSes
        // (observed on US-MA POS 2950). Poll for the Withdrawal button up to 30 s instead
        // of relying on a fixed sleep.
        AppiumElement? withdrawalButton = null;
        for (int waited = 0; waited < 30 && withdrawalButton == null; waited++)
        {
            withdrawalButton = FindWithdrawalButton();
            if (withdrawalButton != null) break;
            Thread.Sleep(1000);
        }
        if (withdrawalButton == null)
        {
            TakeScreenshot("ERROR_NoWithdrawalButton");
            throw new InvalidOperationException("Withdrawal button not found during Z-Report settlement");
        }

        withdrawalButton.Click();
        TakeScreenshot("05_WithdrawalClicked");
        Thread.Sleep(500);
        TapCenterScreen();
        Thread.Sleep(200);

        var amountField = FindAmountField();
        if (amountField == null)
        {
            TakeScreenshot("ERROR_NoAmountField");
            throw new InvalidOperationException("Amount field not found during Z-Report withdrawal");
        }

        int grandTotal = GetTotalSalesAmount();
        amountField.Click();
        amountField.Clear();
        amountField.SendKeys(grandTotal.ToString());
        Thread.Sleep(200);

        var doneButton = FindDoneButton();
        if (doneButton == null)
        {
            TakeScreenshot("ERROR_NoDoneButton");
            throw new InvalidOperationException("Done button not found after entering withdrawal amount");
        }

        doneButton.Click();
        Thread.Sleep(200);
        Console.WriteLine($"✅ Withdrawal of {grandTotal} completed!");

        Thread.Sleep(200);
        var cashInPOSField = FindCashInPOSField();
        if (cashInPOSField == null)
        {
            TakeScreenshot("ERROR_NoCashInPOSField");
            throw new InvalidOperationException("Cash in POS field not found during Z-Report settlement");
        }

        cashInPOSField.Click();
        cashInPOSField.Clear();
        cashInPOSField.SendKeys("5000");
        TakeScreenshot("06_CashInPOSEntered");
        Thread.Sleep(300);
        TapCenterScreen();
        Thread.Sleep(300);

        var cashInPOSDoneButton = FindElementWithTimeout(MobileBy.AccessibilityId("Done"), 3);
        if (cashInPOSDoneButton == null)
        {
            TakeScreenshot("ERROR_NoDoneButtonCashInPOS");
            throw new InvalidOperationException("Done button not found after entering Cash in POS amount");
        }

        cashInPOSDoneButton.Click();
        Thread.Sleep(500);
        TapCenterScreen();
        Thread.Sleep(300);

        Thread.Sleep(500);
        var settlementButton = FindSettlementButton();
        if (settlementButton == null)
        {
            TakeScreenshot("ERROR_NoSettlementButton");
            throw new InvalidOperationException("Settlement button not found");
        }

        settlementButton.Click();
        TakeScreenshot("07_SettlementClicked");
        Thread.Sleep(1000);

        var yesButton = FindYesButton();
        if (yesButton == null)
        {
            TakeScreenshot("ERROR_NoYesButton");
            throw new InvalidOperationException("Yes button not found on settlement confirmation popup");
        }

        yesButton.Click();
        TakeScreenshot("08_ConfirmationYes");
        Thread.Sleep(1000);

        Console.WriteLine("⏳ Waiting for Z-Report to generate (20 seconds)...");
        Thread.Sleep(20000);

        TakeScreenshot("09_ZReportGenerated");
        Console.WriteLine("✅ Z-Report generated and settlement completed!");
    }

    private char _saleTotalDecimalSeparator = ',';

    /// <summary>
    /// Reads the sale total from the "Total 501,00" label. The app formats with the device culture, so the decimal
    /// separator is taken from the label itself (fixed two-decimal display): the separator before the last two digits
    /// is the decimal point, every other separator or (non)breaking space is grouping. The separator found is
    /// remembered for <see cref="FormatAmountForInput"/>.
    /// </summary>
    protected decimal ReadSaleTotal()
    {
        foreach (var el in Driver.FindElements(By.XPath("//XCUIElementTypeStaticText[starts-with(@name,'Total ')]")))
        {
            var m = Regex.Match(el.GetAttribute("name") ?? "", @"^Total\s+([\d\s.,]+)$");
            if (!m.Success) continue;

            var raw = Regex.Replace(m.Groups[1].Value, @"\s", ""); // \s also matches nbsp / narrow nbsp grouping spaces
            var dec = Regex.Match(raw, @"^(.*?)([.,])(\d{2})$");
            if (dec.Success)
            {
                _saleTotalDecimalSeparator = dec.Groups[2].Value[0];
                raw = Regex.Replace(dec.Groups[1].Value, @"[.,]", "") + "." + dec.Groups[3].Value;
            }
            else
            {
                raw = Regex.Replace(raw, @"[.,]", "");
            }
            return decimal.Parse(raw, CultureInfo.InvariantCulture);
        }
        throw new Exception("Sale total label not found");
    }

    /// <summary>
    /// Formats an amount for typing into an app field using the decimal separator the app displayed in the last
    /// <see cref="ReadSaleTotal"/> (i.e. the device culture), so the app parses it to the same value.
    /// </summary>
    protected string FormatAmountForInput(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', _saleTotalDecimalSeparator);

    /// <summary>Best-effort: leaves an open sale (and a possibly open sale-menu popup) by cancelling the sale, so the next test starts from home.</summary>
    protected void TryCancelOpenSale()
    {
        try
        {
            FindElementWithTimeout(MobileBy.AccessibilityId("BtnTotalDiscountClose"), 1)?.Click();
            var more = FindElementWithTimeout(MobileBy.AccessibilityId("btnMore"), 2);
            if (more == null) return;
            more.Click();
            var cancel = FindElementWithTimeout(MobileBy.AccessibilityId("BtnCancelSale"), 3)
                ?? FindElementWithTimeout(By.XPath("//*[@name='Cancel sale']"), 2);
            cancel?.Click();
            Thread.Sleep(1500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cleanup: could not cancel open sale: {ex.Message}");
        }
    }
}
