using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EventPulse.E2E.Tests.Pages;

/// <summary>
/// Page Object for the Login component.
/// </summary>
public class LoginPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public LoginPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(driver, Constants.DefaultWait);
    }

    public void NavigateTo()
    {
        _driver.Navigate().GoToUrl(Constants.BaseUrl + Constants.LoginPath);

        _wait.Until(d => d.FindElement(By.Id("login-email")).Displayed);
    }

    public void EnterEmail(string value) => SetValue("login-email", value);

    public void EnterPassword(string value) => SetValue("login-password", value);

    private void SetValue(string elementId, string value)
    {
        var element = _driver.FindElement(By.Id(elementId));
        element.Clear();
        element.SendKeys(value);
    }

    public void Submit()
    {
        var button = _wait.Until(d =>
        {
            var element = d.FindElement(By.CssSelector("button[type='submit']"));
            return element.Displayed && element.Enabled ? element : null;
        });

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block: 'center', inline: 'center'});", button);

        try
        {
            button.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", button);
        }
    }

    public string? GetFieldError(string fieldElementId)
    {
        try
        {
            var input = _driver.FindElement(By.Id(fieldElementId));

            var errorElements = input.FindElements(By.XPath("following-sibling::p[1]"));
            foreach (var error in errorElements)
            {
                if (error.Displayed && !string.IsNullOrWhiteSpace(error.Text))
                    return error.Text.Trim();
            }

            errorElements = input.FindElements(By.XPath("parent::div/following-sibling::p[1]"));
            foreach (var error in errorElements)
            {
                if (error.Displayed && !string.IsNullOrWhiteSpace(error.Text))
                    return error.Text.Trim();
            }

            return null;
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    /// <summary>
    /// Form-level error only renders when formError is truthy. Skips the
    /// always-present heading block ("Welcome back" / "Sign in to continue...")
    /// which sits in the same preceding-sibling position when no error is shown.
    /// </summary>
    public string? GetFormLevelError(int timeoutSeconds = 10)
    {
        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));

            return wait.Until(d =>
            {
                try
                {
                    var form = d.FindElement(By.TagName("form"));
                    var precedingDiv = form.FindElement(By.XPath("preceding-sibling::div[1]"));

                    var isHeadingWrapper =
                        precedingDiv.FindElements(By.CssSelector("h1.ep-h2")).Count > 0;

                    if (isHeadingWrapper)
                        return null;

                    var text = precedingDiv.Text;
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            });
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
    }

    public bool IsVerifyEmailButtonShown()
    {
        try
        {
            return _driver.FindElements(By.XPath("//button[text()='Verify email']")).Count > 0;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    public bool WaitForRedirectAwayFromLogin(int timeoutSeconds = 10)
    {
        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(d => !d.Url.Contains(Constants.LoginPath));
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }
}