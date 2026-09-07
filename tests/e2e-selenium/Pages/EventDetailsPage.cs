
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EventPulse.E2E.Tests.Pages;

public class EventDetailsPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public EventDetailsPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
    }

    // Page elements

    private By EventTitle =>
        By.CssSelector("h1.ep-h1");

    private By DateAndTime =>
        By.XPath("//span[contains(., 'Date & Time:')]");

    private By Venue =>
        By.XPath("//span[contains(., 'Venue:')]");

    private By AboutEventHeading =>
        By.XPath("//h3[contains(normalize-space(), 'About this Event')]");

    private By EventDescription =>
        By.XPath("//h3[contains(normalize-space(), 'About this Event')]/following-sibling::p");

    private By TicketInformationHeading =>
        By.XPath("//h3[contains(normalize-space(), 'Ticket Information')]");

    private By StartingPrice =>
        By.XPath("//div[contains(normalize-space(), 'Starting Price')]/following-sibling::div");

    private By BackToEventsLink =>
        By.XPath("//a[contains(normalize-space(), 'Back to events')]");

    private By EventNotAvailableHeading =>
        By.XPath("//h2[contains(normalize-space(), 'Event not available')]");

    private By TryAgainButton =>
        By.XPath("//button[contains(normalize-space(), 'Try Again')]");

    // Wait

    public void WaitForPageToLoad()
    {
        _wait.Until(driver =>
            driver.FindElements(EventTitle).Count > 0 ||
            driver.FindElements(EventNotAvailableHeading).Count > 0
        );
    }

    // Verification

    public bool IsEventTitleDisplayed()
    {
        return _driver.FindElements(EventTitle).Count > 0 &&
               _driver.FindElement(EventTitle).Displayed;
    }

    public string GetEventTitle()
    {
        return _driver.FindElement(EventTitle).Text;
    }

    public bool IsDateAndTimeDisplayed()
    {
        return _driver.FindElements(DateAndTime).Count > 0 &&
               _driver.FindElement(DateAndTime).Displayed;
    }

    public bool IsVenueDisplayed()
    {
        return _driver.FindElements(Venue).Count > 0 &&
               _driver.FindElement(Venue).Displayed;
    }

    public bool IsAboutEventDisplayed()
    {
        return _driver.FindElements(AboutEventHeading).Count > 0 &&
               _driver.FindElement(AboutEventHeading).Displayed;
    }

    public bool IsDescriptionDisplayed()
    {
        return _driver.FindElements(EventDescription).Count > 0 &&
               _driver.FindElement(EventDescription).Displayed;
    }

    public bool IsTicketInformationDisplayed()
    {
        return _driver.FindElements(TicketInformationHeading).Count > 0 &&
               _driver.FindElement(TicketInformationHeading).Displayed;
    }

    public bool IsPriceDisplayed()
    {
        return _driver.FindElements(StartingPrice).Count > 0 &&
               _driver.FindElement(StartingPrice).Displayed;
    }

    public string GetPrice()
    {
        return _driver.FindElement(StartingPrice).Text;
    }

    public bool IsBackToEventsDisplayed()
    {
        return _driver.FindElements(BackToEventsLink).Count > 0 &&
               _driver.FindElement(BackToEventsLink).Displayed;
    }

    public void ClickBackToEvents()
    {
        var link = _wait.Until(driver =>
        {
            var elements = driver.FindElements(BackToEventsLink);

            if (elements.Count == 0)
                return null;

            return elements[0].Displayed ? elements[0] : null;
        });

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block: 'center', inline: 'center'});",
            link
        );

        Thread.Sleep(300);

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].click();",
            link
        );
    }

    public bool IsEventNotAvailableDisplayed()
    {
        return _driver.FindElements(EventNotAvailableHeading).Count > 0 &&
               _driver.FindElement(EventNotAvailableHeading).Displayed;
    }

    public bool IsTryAgainDisplayed()
    {
        return _driver.FindElements(TryAgainButton).Count > 0 &&
               _driver.FindElement(TryAgainButton).Displayed;
    }

    public string GetCurrentUrl()
    {
        return _driver.Url;
    }
}

