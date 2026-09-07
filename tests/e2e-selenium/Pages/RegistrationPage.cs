using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;

namespace EventPulse.E2E.Tests.Pages;

/// <summary>
/// Page Object for the Register component.
/// </summary>
public class RegistrationPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    public RegistrationPage(IWebDriver driver)
    {
        _driver = driver;
        _wait = new WebDriverWait(driver, Constants.DefaultWait);
    }

    public void NavigateTo()
    {
        _driver.Navigate().GoToUrl(
            Constants.BaseUrl + Constants.RegisterPath
        );

        _wait.Until(d =>
            d.FindElement(By.Id("reg-firstName")).Displayed
        );
    }

    public void EnterFirstName(string value)
        => SetValue("reg-firstName", value);

    public void EnterLastName(string value)
        => SetValue("reg-lastName", value);

    public void EnterPhone(string value)
        => SetValue("reg-phone", value);

    public void EnterEmail(string value)
        => SetValue("reg-email", value);

    public void EnterPassword(string value)
        => SetValue("reg-password", value);

    public void EnterConfirmPassword(string value)
        => SetValue("reg-confirm", value);

    public void SelectCountry(string countryCode)
    {
        var select = new SelectElement(
            _driver.FindElement(By.Id("reg-country"))
        );

        select.SelectByValue(countryCode);
    }

    private void SetValue(string elementId, string value)
    {
        var element = _driver.FindElement(By.Id(elementId));

        element.Clear();
        element.SendKeys(value);
    }

    public void FillValidForm(string email, string password)
    {
        EnterFirstName("Nimal");
        EnterLastName("Perera");

        SelectCountry("LK");

        EnterPhone("771234567");

        EnterEmail(email);

        EnterPassword(password);
        EnterConfirmPassword(password);
    }

    public void Submit()
    {
        var button = _wait.Until(d =>
        {
            var element = d.FindElement(
                By.CssSelector("button[type='submit']")
            );

            return element.Displayed && element.Enabled
                ? element
                : null;
        });

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({block: 'center', inline: 'center'});",
            button
        );

        try
        {
            button.Click();
        }
        catch (ElementClickInterceptedException)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript(
                "arguments[0].click();",
                button
            );
        }
    }

    public string? GetFieldError(string fieldElementId)
    {
        try
        {
            var input = _driver.FindElement(By.Id(fieldElementId));

            var errorElements = input.FindElements(By.XPath(
                "ancestor::div[.//*[@id='" + fieldElementId + "']][1]/following-sibling::p[1]"
            ));

            foreach (var error in errorElements)
            {
                if (error.Displayed && !string.IsNullOrWhiteSpace(error.Text))
                {
                    return error.Text.Trim();
                }
            }

            errorElements = input.FindElements(By.XPath(
                "parent::div/following-sibling::p[1]"
            ));

            foreach (var error in errorElements)
            {
                if (error.Displayed && !string.IsNullOrWhiteSpace(error.Text))
                {
                    return error.Text.Trim();
                }
            }

            errorElements = input.FindElements(By.XPath(
                "following-sibling::p[1]"
            ));

            foreach (var error in errorElements)
            {
                if (error.Displayed && !string.IsNullOrWhiteSpace(error.Text))
                {
                    return error.Text.Trim();
                }
            }

            return null;
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    public string GetEmailHtml()
    {
        var input = _driver.FindElement(
            By.Id("reg-email")
        );

        return ((IJavaScriptExecutor)_driver).ExecuteScript(
            "return arguments[0].parentElement.outerHTML;",
            input
        )?.ToString() ?? "";
    }

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

    public bool IsSubmitDisabled()
    {
        return _driver
            .FindElement(
                By.CssSelector("button[type='submit']")
            )
            .GetAttribute("disabled") != null;
    }

    public bool WaitForVerifyEmailPage()
    {
        try
        {
            _wait.Until(d =>
                d.Url.Contains(Constants.VerifyEmailPath)
            );

            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }
}