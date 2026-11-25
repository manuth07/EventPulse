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
        try
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                CaptureFailureScreenshot();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaseTest] Failed to capture screenshot on failure: {ex.Message}");
        }
        finally
        {
            try
            {
                Driver?.Quit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BaseTest] Exception during Driver.Quit(): {ex.Message}");
            }
            finally
            {
                Driver?.Dispose();
            }
        }
    }

    private void CaptureFailureScreenshot()
    {
        if (Driver is not ITakesScreenshot screenshotDriver)
            return;

        try
        {
            var screenshot = screenshotDriver.GetScreenshot();

            // Artifact directory: check environment variable or fallback to predictable TestResults/screenshots folder
            var artifactDir = Environment.GetEnvironmentVariable("SELENIUM_SCREENSHOT_DIR");
            if (string.IsNullOrWhiteSpace(artifactDir))
            {
                artifactDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "TestResults", "screenshots");
            }

            Directory.CreateDirectory(artifactDir);

            var testName = TestContext.CurrentContext.Test.Name;
            // Clean invalid filename characters
            var safeTestName = string.Join("_", testName.Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"{safeTestName}_{timestamp}.png";
            var filePath = Path.Combine(artifactDir, filename);

            screenshot.SaveAsFile(filePath);
            Console.WriteLine($"[BaseTest] Failure screenshot saved to: {filePath}");

            // Attach to NUnit TestContext report
            TestContext.AddTestAttachment(filePath, "Failure Screenshot");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BaseTest] Error saving screenshot: {ex.Message}");
        }
    }
}
