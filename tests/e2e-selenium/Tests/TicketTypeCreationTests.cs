using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;

public class TicketTypeCreationTests
{
    private const string EventId =
        "6766a80c-b15e-4c92-9b84-c9c36f8a566c";

    private const string EventName =
        "CONCEPT COLLECTIVE EP 2";

    [Fact]
    public void Organizer_CreatesTicketType_AndItPersistsAfterRefresh()
    {
        string email = Required("EVENTPULSE_QA_EMAIL");
        string password = Required("EVENTPULSE_QA_PASSWORD");

        string loginUrl = Required("EVENTPULSE_LOGIN_URL");
        string eventUrl = Required("EVENTPULSE_TICKET_PAGE_URL");

        // Change these if EP-39 requires different valid test values.
        const string price = "2500";
        const string capacity = "7";

        string ticketName = "QA-Selenium-" +
            Guid.NewGuid().ToString("N")[..8];

        IWebDriver driver = new ChromeDriver();

        try
        {
            var wait = new WebDriverWait(
                driver, TimeSpan.FromSeconds(20));

            // 1. Log in.
            driver.Navigate().GoToUrl(loginUrl);

            Visible(wait, By.Id("login-email"))
                .SendKeys(email);

            Visible(wait, By.Id("login-password"))
                .SendKeys(password);

            Visible(
                wait,
                By.XPath(
                    "//form//button[@type='submit' " +
                    "and normalize-space()='Sign In']"))
                .Click();

            // Wait for successful navigation away from login.
            wait.Until(d =>
                !d.Url.Contains("/login",
                    StringComparison.OrdinalIgnoreCase));

            // 2. Open the organizer-owned event.
            driver.Navigate().GoToUrl(eventUrl);

            Console.WriteLine("URL after opening event: " + driver.Url);
        Console.WriteLine(
                           "Visible main elements: " +
                            driver.FindElements(By.TagName("main"))
        .Count(e => e.Displayed));

Assert.Contains(EventId, driver.Url);

wait.Until(d =>
    d.FindElements(By.TagName("main"))
        .Any(e => e.Displayed));

Assert.Contains(EventName, driver.PageSource);

            // 3. Find ONLY the Create form—not the VIP Edit form.
            IWebElement nameBox = Visible(
                wait,
                By.CssSelector(
                    "form input[placeholder=" +
                    "'e.g. VIP, General Admission']"));

            IWebElement priceBox = Visible(
                wait,
                By.CssSelector(
                    "form input[type='number']" +
                    "[placeholder='0']"));

            IWebElement capacityBox = Visible(
                wait,
                By.CssSelector(
                    "form input[type='number']" +
                    "[placeholder='e.g. 50']"));

            nameBox.Clear();
            nameBox.SendKeys(ticketName);

            priceBox.Clear();
            priceBox.SendKeys(price);

            capacityBox.Clear();
            capacityBox.SendKeys(capacity);

            Visible(
                wait,
                By.XPath(
                    "//form//button[@type='submit' " +
                    "and normalize-space()='Save Ticket Type']"))
                .Click();

            // 4. Confirm the newly created card displays the data.
            string cardBeforeRefresh =
                SavedTicketCardText(wait, ticketName);

            Assert.Contains(ticketName, cardBeforeRefresh);
            Assert.Contains(price,
                cardBeforeRefresh.Replace(",", ""));
            Assert.Contains(capacity, cardBeforeRefresh);

            // 5. Reload: a ticket held only in React memory will disappear.
            driver.Navigate().Refresh();

            string cardAfterRefresh =
                SavedTicketCardText(wait, ticketName);

            Assert.Contains(ticketName, cardAfterRefresh);
            Assert.Contains(price,
                cardAfterRefresh.Replace(",", ""));
            Assert.Contains(capacity, cardAfterRefresh);

            Assert.Contains(EventId, driver.Url);
            Assert.Contains(EventName, driver.PageSource);
        }
        finally
        {
            driver.Quit();
        }
    }

    private static IWebElement Visible(
        WebDriverWait wait, By locator)
    {
        return wait.Until(d =>
            d.FindElements(locator)
                .FirstOrDefault(e => e.Displayed))!;
    }

    private static string SavedTicketCardText(
        WebDriverWait wait, string ticketName)
    {
        // The HTML you sent shows saved ticket names in a span.
        // Its parent row also contains "LKR ... • ... available".
        By savedName = By.XPath(
            "//span[normalize-space()='" + ticketName + "']");

        return wait.Until(d =>
        {
            IWebElement? name = d.FindElements(savedName)
                .FirstOrDefault(e => e.Displayed);

            return name?.FindElement(By.XPath("..")).Text;
        })!;
    }

    private static string Required(string variable)
    {
        return Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException(
                $"Missing {variable}. Set it before dotnet test.");
    }
}