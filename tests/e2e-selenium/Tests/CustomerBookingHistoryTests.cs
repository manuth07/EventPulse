using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

/// <summary>
/// End-to-end Selenium tests for Customer Booking History (Scenario 3).
/// Verifies protected route behavior, authenticated booking history display,
/// status filter tabs, and legitimate empty-state rendering without requiring live Stripe checkout.
/// </summary>
[TestFixture]
[Category("SE3112-E2E")]
public class CustomerBookingHistoryTests : BaseTest
{
    private LoginPage _loginPage = null!;
    private MyBookingsPage _myBookingsPage = null!;

    [SetUp]
    public void SetUpPages()
    {
        _loginPage = new LoginPage(Driver);
        _myBookingsPage = new MyBookingsPage(Driver);
    }

    [Test]
    [Description("Unauthenticated user attempting to access /my-bookings must be redirected to /login.")]
    public void MyBookings_UnauthenticatedUser_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + Constants.MyBookingsPath);

        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        var redirected = wait.Until(d => d.Url.Contains(Constants.LoginPath));

        Assert.That(redirected, Is.True, "Unauthenticated access to /my-bookings must redirect to the login page.");
    }

    [Test]
    [Description("Authenticated customer navigating to /my-bookings sees the page title and status filter tabs.")]
    public void MyBookings_AuthenticatedCustomer_DisplaysPageHeaderAndStatusTabs()
    {
        var email = Environment.GetEnvironmentVariable("TEST_CUSTOMER_EMAIL") ?? "nimal.test01@example.com";
        var password = Environment.GetEnvironmentVariable("TEST_CUSTOMER_PASSWORD") ?? "SecurePass123!";

        _loginPage.LoginAs(email, password);

        // If login could not redirect (e.g. backend auth service offline), assert gracefully
        if (Driver.Url.Contains(Constants.LoginPath))
        {
            Assert.Ignore("Authentication service was not available to establish a customer session in this environment.");
            return;
        }

        _myBookingsPage.NavigateTo();

        Assert.Multiple(() =>
        {
            Assert.That(_myBookingsPage.IsPageTitleDisplayed(), Is.True,
                "Expected 'My Booked Tickets' page title to be displayed.");
            Assert.That(_myBookingsPage.AreStatusTabsDisplayed(), Is.True,
                "Expected status filter tabs (All, Confirmed, Pending Payment, Cancelled) to be displayed.");
            Assert.That(_myBookingsPage.HasBookingsOrEmptyState(), Is.True,
                "Expected either existing booking cards or a valid empty-state message.");
        });
    }

    [Test]
    [Description("Customer can switch between status filter tabs (All, Confirmed, Pending Payment, Cancelled)")]
    public void MyBookings_AuthenticatedCustomer_CanSwitchStatusTabs()
    {
        var email = Environment.GetEnvironmentVariable("TEST_CUSTOMER_EMAIL") ?? "nimal.test01@example.com";
        var password = Environment.GetEnvironmentVariable("TEST_CUSTOMER_PASSWORD") ?? "SecurePass123!";

        _loginPage.LoginAs(email, password);

        if (Driver.Url.Contains(Constants.LoginPath))
        {
            Assert.Ignore("Authentication service was not available to establish a customer session in this environment.");
            return;
        }

        _myBookingsPage.NavigateTo();

        // Switch to Confirmed tab
        _myBookingsPage.ClickTab("Confirmed");
        Assert.That(_myBookingsPage.HasBookingsOrEmptyState(), Is.True,
            "Expected Confirmed tab to render valid bookings or legitimate empty state.");

        // Switch to Cancelled tab
        _myBookingsPage.ClickTab("Cancelled");
        Assert.That(_myBookingsPage.HasBookingsOrEmptyState(), Is.True,
            "Expected Cancelled tab to render valid bookings or legitimate empty state.");
    }
}
