using System.Globalization;
using LocalVenue.Tests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace LocalVenue.Tests.Selenium;

public class EndToEndTest : IDisposable
{
    public IWebDriver driver { get; private set; }
    public IDictionary<String, Object> vars { get; private set; }
    public IJavaScriptExecutor js { get; private set; }
    private string baseUrl = "https://localvenue-webapp-casp0006.azurewebsites.net";

    public EndToEndTest()
    {
        driver = new ChromeDriver();
        js = (IJavaScriptExecutor)driver;
        vars = new Dictionary<String, Object>();
    }

    public void Dispose()
    {
        driver.Quit();
    }

    [IgnoreOnBuildServerFact]
    public async Task EndToEnd()
    {
        await driver.Navigate().GoToUrlAsync(baseUrl);
        driver.Manage().Window.Size = new System.Drawing.Size(1936, 1048);
        driver.FindElement(By.Id("main-layout-login-link")).Click();

        WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));

        wait.Until(d => d.Url.Contains("/Login"));

        wait.Until(ExpectedConditions.ElementToBeClickable(By.Id("email")));

        driver.FindElement(By.Id("email")).Click();
        driver.FindElement(By.Id("email")).SendKeys("ADMIN@HOTMAIL.COM");
        driver.FindElement(By.Id("password")).Click();
        driver.FindElement(By.Id("password")).SendKeys("123");

        var loginButton = driver.FindElement(By.Id("login-button"));

        loginButton.Submit();

        wait.Until(d => d.Url.Contains(baseUrl));

        var showLinkButton = wait.Until(
            ExpectedConditions.ElementToBeClickable(By.LinkText("Shows"))
        );

        showLinkButton.Click();

        var createShowButton = wait.Until(
            ExpectedConditions.ElementToBeClickable(By.Id("create-show-button"))
        );

        createShowButton.Click();

        wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".modal-body ")));

        driver.FindElement(By.Id("show-title-input")).Click();
        driver.FindElement(By.Id("show-title-input")).SendKeys("Test End to end");
        driver.FindElement(By.Id("show-genre-input")).Click();
        {
            var dropdown = driver.FindElement(By.Id("show-genre-input"));
            dropdown.FindElement(By.XPath("//option[. = 'Romantik']")).Click();
        }
        driver.FindElement(By.Id("show-description-input")).Click();
        driver.FindElement(By.Id("show-description-input")).SendKeys("Test for end to end ;)");
        driver.FindElement(By.Id("show-start-time-input")).Click();
        driver
            .FindElement(By.Id("show-start-time-input"))
            .SendKeys(DateTime.Today.AddDays(1).AddHours(16).ToString(CultureInfo.CurrentCulture));
        driver.FindElement(By.Id("show-end-time-input")).Click();
        driver
            .FindElement(By.Id("show-end-time-input"))
            .SendKeys(DateTime.Today.AddDays(1).AddHours(18).ToString(CultureInfo.CurrentCulture));
        driver.FindElement(By.CssSelector(".form-check-label")).Click();

        driver.FindElement(By.Id("create-or-save-changes-button")).Submit();

        await Task.Delay(2000);

        var homeButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.LinkText("Home")));

        homeButton.Click();

        wait.Until(d => d.Url.Contains(baseUrl));
        wait.Until(ExpectedConditions.ElementExists(By.Id("home-shows-grid")));

        var show = driver
            .FindElement(By.Id("home-shows-grid"))
            .FindElement(By.CssSelector(".col:last-child"));

        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", show);

        wait.Until(webDriver =>
            (bool)
                ((IJavaScriptExecutor)webDriver).ExecuteScript(
                    "return arguments[0].getBoundingClientRect().top >= 0 && arguments[0].getBoundingClientRect().bottom <= window.innerHeight;",
                    show
                )
        );

        show.Click();

        wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".seat-display")));

        for (int i = 1; i < 5; i++)
        {
            var seatFirstRow = driver
                .FindElement(By.CssSelector(".seat-display"))
                .FindElement(
                    By.CssSelector($".seat-row:nth-child(1) > .seat:nth-child({i * 2 - 1})")
                );

            seatFirstRow.Click();

            var addTicketButton = driver
                .FindElement(By.CssSelector(".seat-display"))
                .FindElement(
                    By.CssSelector($".seat-row:nth-child(1) > .ui-popover:nth-child({i * 2})")
                )
                .FindElement(By.CssSelector(".btn"));

            addTicketButton.Click();

            driver.FindElement(By.TagName("h1")).Click();

            await Task.Delay(500);
        }

        var header = driver.FindElement(By.TagName("h1"));
        var description = driver.FindElement(By.Id("show-description"));

        Assert.Equal("Test End to end", header.Text);
        Assert.Equal("Test for end to end ;)", description.Text);

        driver.FindElement(By.Id("buy-tickets-button")).Click();

        await Task.Delay(2000);

        driver.FindElement(By.LinkText("Shows")).Click();

        var deleteButton = wait.Until(
            ExpectedConditions.ElementExists(
                By.CssSelector(".column-body-item-hover:last-child .btn-danger")
            )
        );

        ((IJavaScriptExecutor)driver).ExecuteScript(
            "arguments[0].scrollIntoView(true);",
            deleteButton
        );

        wait.Until(webDriver =>
            (bool)
                ((IJavaScriptExecutor)webDriver).ExecuteScript(
                    "return arguments[0].getBoundingClientRect().top >= 0 && arguments[0].getBoundingClientRect().bottom <= window.innerHeight;",
                    deleteButton
                )
        );

        deleteButton.Click();

        wait.Until(ExpectedConditions.ElementIsVisible(By.Id("delete-show-button-in-modal")));

        driver.FindElement(By.CssSelector("td:nth-child(1)")).Click();
        driver.FindElement(By.CssSelector(".btn-danger:nth-child(2)")).Click();

        await Task.Delay(2000);
    }
}
