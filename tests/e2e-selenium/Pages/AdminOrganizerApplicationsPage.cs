using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;

namespace EventPulse.E2E.Tests.Pages;

public class AdminOrganizerApplicationsPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public AdminOrganizerApplicationsPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(driver, Constants.DefaultWait);
    }

    public void NavigateTo()
    {
        _driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin/organizer-applications");

        try
        {
            _wait.Until(d => d.FindElements(By.XPath("//h1[contains(text(),'Organizer Applications')]")).Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException(
                $"Expected to land on /admin/organizer-applications but got stuck elsewhere. " +
                $"Actual URL: {_driver.Url} | Page title: {_driver.Title}");
        }
    }

    private By ReviewButtons => By.XPath("//button[.//span[text()='Review']]");
    private By EmptyStateHeading => By.XPath("//h3[contains(text(),'applications')]");
    // The error banner renders AlertCircle + a message span instead of the list/empty-state —
    // this is a real, distinct UI state the earlier version of this page object never checked for.
    private By ErrorBanner => By.XPath("//span[contains(text(),'Failed to load') or contains(text(),'token not found') or contains(text(),'Unauthorized')]");
    private By LoadingSpinner => By.XPath("//p[contains(text(),'Loading organizer applications')]");

    public void WaitForListOrEmptyState()
    {
        try
        {
            _wait.Until(d =>
                d.FindElements(ReviewButtons).Count > 0 ||
                d.FindElements(EmptyStateHeading).Count > 0 ||
                d.FindElements(ErrorBanner).Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            // Timed out with NONE of the three known states present — most likely the
            // loading spinner never resolved (a hung/never-completing fetch). Surface
            // what's actually on screen instead of a bare "timed out" message.
            var stillLoading = _driver.FindElements(LoadingSpinner).Count > 0;
            var bodySnippet = _driver.FindElement(By.TagName("main")).Text;
            if (bodySnippet.Length > 400) bodySnippet = bodySnippet[..400] + "...";
            throw new WebDriverTimeoutException(
                $"Neither the application list, empty state, nor an error banner appeared. " +
                $"Still showing loading spinner: {stillLoading}. " +
                $"Visible <main> content: \"{bodySnippet}\"");
        }

        // If we got here via the error banner specifically, fail now with the REAL message
        // instead of letting the test proceed and fail confusingly two steps later.
        var errorElements = _driver.FindElements(ErrorBanner);
        if (errorElements.Count > 0)
        {
            throw new AssertionException(
                $"AdminOrganizerApplications page loaded but shows an error instead of data: \"{errorElements[0].Text}\"");
        }
    }

    public int GetReviewButtonCount() => _driver.FindElements(ReviewButtons).Count;
    public bool IsEmptyStateDisplayed() => _driver.FindElements(EmptyStateHeading).Count > 0;

    public void ClickFirstReview()
    {
        var button = _wait.Until(d => d.FindElements(ReviewButtons).FirstOrDefault(b => b.Displayed));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", button);
        button!.Click();
    }

    public void ClickApproveTab()
    {
        var button = _wait.Until(d => d.FindElement(By.XPath("//button[contains(normalize-space(),'Approve & Grant Organizer Role')]")));
        button.Click();
    }

    public void ClickRejectTab()
    {
        var button = _wait.Until(d => d.FindElement(By.XPath("//button[contains(normalize-space(),'Reject Application')]")));
        button.Click();
    }

    public void EnterRejectComment(string comment)
    {
        var textarea = _driver.FindElement(By.CssSelector("textarea"));
        textarea.Clear();
        textarea.SendKeys(comment);
    }

    public void ConfirmReject()
    {
        var button = _driver.FindElement(By.XPath("//button[contains(normalize-space(),'Confirm Rejection')]"));
        button.Click();
    }

    public void ConfirmApprove()
    {
        var button = _driver.FindElement(By.XPath("//button[contains(normalize-space(),'Confirm Approval')]"));
        button.Click();
    }

    public bool WaitForFeedbackBanner(int timeoutSeconds = 8)
    {
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds))
                .Until(d => d.FindElements(By.XPath("//span[contains(text(),'granted') or contains(text(),'rejected')]")).Count > 0);
            return true;
        }
        catch (WebDriverTimeoutException) { return false; }
    }
}