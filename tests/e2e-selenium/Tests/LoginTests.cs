using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class LoginTests : BaseTest
{
    private LoginPage _page = null!;

    [SetUp]
    public void SetUpPage()
    {
        _page = new LoginPage(Driver);
        _page.NavigateTo();
    }

    [Test]
    public void Login_WithEmptyForm_ShowsRequiredFieldErrors()
    {
        _page.Submit();

        Assert.Multiple(() =>
        {
            Assert.That(_page.GetFieldError("login-email"), Is.EqualTo("Email address is required."));
            Assert.That(_page.GetFieldError("login-password"), Is.EqualTo("Password is required."));
        });
    }

    [Test]
    public void Login_WithInvalidEmailFormat_ShowsEmailError()
    {
        _page.EnterEmail("not-an-email");
        _page.EnterPassword("SomePassword123!");
        _page.Submit();

        Assert.That(_page.GetFieldError("login-email"), Is.EqualTo("Enter a valid email address."));
    }

    [Test]
    public void Login_WithWrongCredentials_ShowsInvalidLoginError()
    {
        _page.EnterEmail($"nonexistent.{Guid.NewGuid():N}@example.com");
        _page.EnterPassword("WrongPassword999!");
        _page.Submit();

        var error = _page.GetFormLevelError(timeoutSeconds: 10);

        Assert.That(error, Is.EqualTo("Invalid email or password."));
    }

    // -----------------------------------------------------------------
    // BLOCKED — see BUG-07 (JWT signing key not configured, undocumented)
    // Successful login requires JWT generation, which currently 500s.
    // -----------------------------------------------------------------
    [Test]
    [Ignore("Blocked by BUG-07: JWT signing key not configured locally/undocumented. Remove Ignore once resolved for all environments.")]
    public void Login_WithValidVerifiedCredentials_RedirectsAwayFromLoginPage()
    {
        _page.EnterEmail("nimal.test01@example.com");
        _page.EnterPassword("SecurePass123!");
        _page.Submit();

        var redirected = _page.WaitForRedirectAwayFromLogin();

        Assert.That(redirected, Is.True, "Expected redirect away from /login after successful authentication.");
    }
}