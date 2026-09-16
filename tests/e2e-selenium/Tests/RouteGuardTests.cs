using NUnit.Framework;
using OpenQA.Selenium;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class RouteGuardTests : BaseTest
{
    [Test]
    public void UnauthenticatedUser_AccessingOrganizerRoute_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/organizer");
        Assert.That(Driver.Url, Does.Contain("/login"),
            "RequireRole should redirect unauthenticated users to /login, preserving 'from' in route state.");
    }

    [Test]
    public void UnauthenticatedUser_AccessingAdminRoute_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin");
        Assert.That(Driver.Url, Does.Contain("/login"));
    }

    [Test]
    public void UnauthenticatedUser_AccessingCreateEventRoute_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/events/create");
        Assert.That(Driver.Url, Does.Contain("/login"));
    }

    // -----------------------------------------------------------------
    // The tests below require a logged-in Customer session first — log in
    // via LoginPage, then attempt the protected navigation, then assert
    // redirect to /forbidden. Wire up once BUG-07 is confirmed resolved
    // and you have a verified Customer test account.
    // -----------------------------------------------------------------
    [Test]
    
    public void CustomerRole_AccessingOrganizerRoute_RedirectsToForbidden()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("nimal.test01@example.com");
        loginPage.EnterPassword("SecurePass123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/organizer");

        Assert.That(Driver.Url, Does.Contain("/forbidden"),
            "RequireRole should redirect an authenticated but wrong-role user to /forbidden, not /login.");
    }

    [Test]
    public void CustomerRole_AccessingAdminRoute_RedirectsToForbidden()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("nimal.test01@example.com");
        loginPage.EnterPassword("SecurePass123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin");

        Assert.That(Driver.Url, Does.Contain("/forbidden"));
    }

    [Test]
    public void UnauthenticatedUser_AccessingAdminPendingEventsRoute_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin/events/pending");

        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        wait.Until(d => d.Url.Contains("/login"));

        Assert.That(Driver.Url, Does.Contain("/login"));
    }

    [Test]
    public void UnauthenticatedUser_AccessingAdminOrganizerApplicationsRoute_RedirectsToLogin()
    {
        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin/organizer-applications");

        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(5));
        wait.Until(d => d.Url.Contains("/login"));

        Assert.That(Driver.Url, Does.Contain("/login"));
    }

    [Test]
    
    public void OrganizerRole_AccessingAdminRoute_RedirectsToForbidden()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("organizer@eventpulse.dev");
        loginPage.EnterPassword("Organizer123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/admin");

        Assert.That(Driver.Url, Does.Contain("/forbidden"),
            "An Organizer must not be able to reach Administrator-only routes.");
    }

    [Test]
    
    public void AdministratorRole_AccessingOrganizerRoute_RedirectsToForbidden()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("admin@eventpulse.com");
        loginPage.EnterPassword("Admin123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        Driver.Navigate().GoToUrl(Constants.BaseUrl + "/organizer");

        Assert.That(Driver.Url, Does.Contain("/forbidden"),
            "An Administrator without the Customer/Organizer role must not reach Organizer-only routes.");
    }
}