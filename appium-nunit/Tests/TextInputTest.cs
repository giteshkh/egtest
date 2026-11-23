using BStackAppiumNUnit.Helpers;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Support.UI;

namespace BStackAppiumNUnit.Tests;

[TestFixture]
[Category("Sample")]
public class TextInputTest : BaseTest
{
    private static string LoadInputText()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "text-input.json");
        return JObject.Parse(File.ReadAllText(path))["inputText"]!.Value<string>()!;
    }

    [Test]
    public void Should_ShowEnteredText_InOutputLabel()
    {
        var inputText = LoadInputText();

        // Locators verified on a live iOS session of the BrowserStack sample app
        WaitFor("Text Button").Click();

        var input = WaitFor("Text Input");
        input.SendKeys(inputText);
        WaitFor("Return").Click();

        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(DefaultTimeoutSeconds))
        {
            Message = $"Text Output should show \"{inputText}\" after submitting the text"
        };
        wait.Until(d => WaitFor("Text Output").Text == inputText);

        Assert.That(WaitFor("Text Output").Text, Is.EqualTo(inputText),
            "Text Output should equal the entered text");
    }
}
