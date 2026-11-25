using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EventPulse.E2E.Tests.Pages;

/// <summary>
/// Page Object for the Customer My Bookings page (/my-bookings).
/// </summary>
public class MyBookingsPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public MyBookingsPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(driver, Constants.DefaultWait);
    }

    // Selectors using stable data-testid attributes defined in MyBookingsPage.jsx
    private By PageTitle => By.CssSelector("[data-testid='my-bookings-page-title']");
    private By StatusFilterTabs => By.CssSelector("[data-testid='status-filter-tabs']");
    private By EmptyState => By.CssSelector("[data-testid='bookings-empty-state']");
    private By LoadingState => By.CssSelector("[data-testid='bookings-loading-state']");
    private By ErrorState => By.CssSelector("[data-testid='bookings-error-state']");
    private By BookingCards => By.CssSelector("[data-testid^='booking-card-']");
    private By ExploreEventsButton => By.CssSelector("[data-testid='explore-events-btn']");

    public void NavigateTo()
    {
        _driver.Navigate().GoToUrl(Constants.BaseUrl + Constants.MyBookingsPath);
        WaitForPageToLoad();
    }

    public void WaitForPageToLoad()
    {
        // Wait until either page title appears or we get redirected
        _wait.Until(d =>
            d.FindElements(PageTitle).Count > 0 ||
            d.FindElements(EmptyState).Count > 0 ||
            d.Url.Contains(Constants.LoginPath));

        // If loading spinner is visible, wait for it to disappear
        try
        {
            var shortWait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            shortWait.Until(d => d.FindElements(LoadingState).Count == 0);
        }
        catch (WebDriverTimeoutException)
        {
            // Ignore timeout if loading element resolved quickly or wasn't rendered
        }
    }

    public bool IsPageTitleDisplayed()
    {
        return _driver.FindElements(PageTitle).Count > 0 &&
               _driver.FindElement(PageTitle).Displayed;
    }

    public string GetPageTitleText()
    {
        return _driver.FindElement(PageTitle).Text.Trim();
    }

    public bool AreStatusTabsDisplayed()
    {
        return _driver.FindElements(StatusFilterTabs).Count > 0 &&
               _driver.FindElement(StatusFilterTabs).Displayed;
    }

    public bool IsEmptyStateDisplayed()
    {
        return _driver.FindElements(EmptyState).Count > 0 &&
               _driver.FindElement(EmptyState).Displayed;
    }

    public int GetBookingCardCount()
    {
        return _driver.FindElements(BookingCards).Count;
    }

    public bool HasBookingsOrEmptyState()
    {
        return GetBookingCardCount() > 0 || IsEmptyStateDisplayed();
    }

    public void ClickTab(string tabValue)
    {
        var tabSelector = By.CssSelector($"[data-testid='filter-tab-{tabValue}']");
        var tab = _wait.Until(d =>
        {
            var el = d.FindElement(tabSelector);
            return el.Displayed && el.Enabled ? el : null;
        })!;

        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", tab);
        _wait.Until(d => tab.Displayed && tab.Enabled);
        tab.Click();
    }
}
