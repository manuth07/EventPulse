
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EventPulse.E2E.Tests.Pages;

public class BrowseEventsPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public BrowseEventsPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
    }

    // Page elements

    private By UpcomingEventsHeading =>
        By.XPath("//h2[contains(normalize-space(), 'Upcoming Events')]");

    private By EventCards =>
    By.XPath("//div[contains(@class, 'ep-card')][.//a[contains(normalize-space(), 'View Details')]]");

    private By ViewDetailsLinks =>
    By.XPath("//a[contains(normalize-space(), 'View Details')]");

    private By ClearSearchButton =>
        By.XPath("//button[contains(normalize-space(), 'Clear Search')]");

    // Actions

    public void NavigateToHome(string baseUrl)
    {
        _driver.Navigate().GoToUrl(baseUrl);
    }

    public void WaitForPageToLoad()
    {
        _wait.Until(driver =>
            driver.FindElements(UpcomingEventsHeading).Count > 0
        );

        _wait.Until(driver =>
        {
            var cards = driver.FindElements(EventCards);
            var links = driver.FindElements(ViewDetailsLinks);

            return cards.Count > 0 ||
                links.Count > 0 ||
                driver.PageSource.Contains("No events") ||
                driver.PageSource.Contains("events available");
        });
    }

    public bool IsUpcomingEventsHeadingDisplayed()
    {
        return _driver.FindElements(UpcomingEventsHeading).Count > 0 &&
               _driver.FindElement(UpcomingEventsHeading).Displayed;
    }

    public int GetEventCardCount()
    {
        return _driver.FindElements(EventCards).Count;
    }

    public int GetViewDetailsLinkCount()
    {
        return _driver.FindElements(ViewDetailsLinks).Count;
    }

    public void ClickFirstEventDetails()
    {
        var link = _wait.Until(driver =>
        {
            var elements = driver.FindElements(ViewDetailsLinks);

            if (elements.Count == 0)
                return null;

            var firstLink = elements[0];

            if (!firstLink.Displayed || !firstLink.Enabled)
                return null;

            return firstLink;
        });

        // Bring the link into the visible part of the viewport.
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block: 'center', inline: 'center'});",
            link
        );

        // Small wait for scrolling/layout to settle.
        Thread.Sleep(300);

        try
        {
            link.Click();
        }
        catch (ElementClickInterceptedException)
        {
            // Fallback for cases where another element overlaps the link.
            ((IJavaScriptExecutor)_driver).ExecuteScript(
                "arguments[0].click();",
                link
            );
        }
    }

    public bool IsClearSearchButtonDisplayed()
    {
        return _driver.FindElements(ClearSearchButton).Count > 0 &&
               _driver.FindElement(ClearSearchButton).Displayed;
    }

    public string GetCurrentUrl()
    {
        return _driver.Url;
    }
}

