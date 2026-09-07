using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EventPulse.E2E.Tests.Pages;

public class CreateEventPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public CreateEventPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(driver, Constants.DefaultWait);
    }

    public void NavigateTo()
    {
        _driver.Navigate().GoToUrl(Constants.BaseUrl + "/events/create");
        _wait.Until(d => d.FindElement(By.CssSelector("form")).Displayed);
    }

    private IWebElement TitleInput => _driver.FindElement(By.XPath("//label[contains(text(),'Event Title')]/following-sibling::input"));
    private IWebElement DescriptionInput => _driver.FindElement(By.XPath("//label[contains(text(),'Description')]/following-sibling::textarea"));
    private IWebElement VenueInput => _driver.FindElement(By.XPath("//label[contains(text(),'Venue Location')]/following-sibling::div//input"));
    private IWebElement EventDateInput => _driver.FindElement(By.CssSelector("input[type='datetime-local']"));
    private IWebElement PriceInput => _driver.FindElement(By.CssSelector("input[type='number']"));

    // Located relative to each section's own heading text, NOT by array index —
    // the poster's <input type="file"> is removed from the DOM once uploaded
    // (replaced by a preview <img>), which breaks any index-based lookup.
    private IWebElement PosterFileInput =>
        _driver.FindElement(By.XPath("//label[contains(text(),'Event Poster')]/following::input[@type='file'][1]"));
    private IWebElement CoverFileInput =>
        _driver.FindElement(By.XPath("//label[contains(text(),'Event Cover')]/following::input[@type='file'][1]"));

    private By SubmitButton => By.XPath("//button[@type='submit']");
    private By ErrorBanner => By.XPath("//div[contains(., 'Please provide an event')]");

    public string? GetErrorMessage()
    {
        try { return _driver.FindElement(ErrorBanner).Text; }
        catch (NoSuchElementException) { return null; }
    }
    private By SuccessBanner => By.XPath("//h3[contains(text(),'Event Submitted Successfully')]");

    public void FillRequiredFields(string title, string description, string venue, DateTime eventDate, string price)
    {
        TitleInput.Clear(); TitleInput.SendKeys(title);
        DescriptionInput.Clear(); DescriptionInput.SendKeys(description);
        VenueInput.Clear(); VenueInput.SendKeys(venue);

        // React-controlled input: setting .value directly never fires onChange,
        // so React's internal state never updates and the native `required`
        // attribute silently blocks form submission with no visible error.
        // This writes through the native property setter and dispatches a real
        // 'input' event so React's onChange listener actually fires.
        var js = (IJavaScriptExecutor)_driver;
        js.ExecuteScript(@"
            const input = arguments[0];
            const value = arguments[1];
            const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
            nativeSetter.call(input, value);
            input.dispatchEvent(new Event('input', { bubbles: true }));
        ", EventDateInput, eventDate.ToString("yyyy-MM-ddTHH:mm"));

        PriceInput.Clear(); PriceInput.SendKeys(price);
    }

    public void UploadPoster(string absoluteFilePath) => PosterFileInput.SendKeys(absoluteFilePath);
    public void UploadCover(string absoluteFilePath) => CoverFileInput.SendKeys(absoluteFilePath);

    public void Submit()
    {
        var button = _wait.Until(d => d.FindElement(SubmitButton));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", button);
        System.Threading.Thread.Sleep(300);
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", button);
    }

    // Retries for up to 5s instead of checking once immediately — React's
    // setError() state update needs a render cycle to actually appear in the DOM,
    // and a single-shot check right after Submit() can race that render.
    public string? GetErrorMessage(int timeoutSeconds = 5)
    {
        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            var element = wait.Until(d =>
            {
                try { return d.FindElement(ErrorBanner); }
                catch (NoSuchElementException) { return null; }
            });
            return element?.Text;
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
    }

    public bool WaitForSuccessBanner(int timeoutSeconds = 5)
    {
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(d => d.FindElement(SuccessBanner).Displayed);
            return true;
        }
        catch (WebDriverTimeoutException) { return false; }
    }

    public bool WaitForRedirectToOrganizerDashboard(int timeoutSeconds = 5)
    {
        try
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(d => d.Url.EndsWith("/organizer"));
            return true;
        }
        catch (WebDriverTimeoutException) { return false; }
    }
}