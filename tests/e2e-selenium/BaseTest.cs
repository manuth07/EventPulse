using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EventPulse.E2E.Tests;

public abstract class BaseTest
{
    protected IWebDriver Driver = null!;

    [SetUp]
    public void SetUpDriver()
    {
        var options = new ChromeOptions();
        // Comment out --headless while debugging locally so you can watch the browser.
        options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1280,900");

        // Selenium Manager (bundled in Selenium.WebDriver 4.6+) resolves the
        // matching chromedriver automatically — no manual driver download needed,
        // as long as Chrome itself is installed on this machine.
        Driver = new ChromeDriver(options);
    }

    [TearDown]
    public void TearDownDriver()
    {
        Driver?.Quit();
        Driver?.Dispose();
    }
}
