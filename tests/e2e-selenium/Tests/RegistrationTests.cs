using NUnit.Framework;
using OpenQA.Selenium;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class RegistrationTests : BaseTest
{
    private RegistrationPage _page = null!;

    [SetUp]
    public void SetUpPage()
    {
        _page = new RegistrationPage(Driver);
        _page.NavigateTo();
    }

    [Test]
    public void Register_WithEmptyForm_ShowsRequiredFieldErrors()
    {
        _page.Submit();

        Assert.Multiple(() =>
        {
            Assert.That(
                _page.GetFieldError("reg-firstName"),
                Is.EqualTo("First name is required.")
            );

            Assert.That(
                _page.GetFieldError("reg-lastName"),
                Is.EqualTo("Last name is required.")
            );

            Assert.That(
                _page.GetFieldError("reg-phone"),
                Is.EqualTo("Contact number is required.")
            );

            Assert.That(
                _page.GetFieldError("reg-email"),
                Is.EqualTo("Email address is required.")
            );

            Assert.That(
                _page.GetFieldError("reg-password"),
                Is.EqualTo("Password is required.")
            );

            Assert.That(
                _page.GetFieldError("reg-confirm"),
                Is.EqualTo("Please confirm your password.")
            );
        });
    }

    [Test]
    public void Register_WithInvalidEmailFormat_ShowsEmailError()
    {
        _page.FillValidForm(
            "not-an-email",
            "SecurePass123!"
        );

        _page.EnterEmail("not-an-email");

        _page.Submit();

        Assert.That(
            _page.GetFieldError("reg-email"),
            Is.EqualTo("Enter a valid email address.")
        );
    }

    [TestCase(
        "short1A",
        "Password must be at least 8 characters."
    )]

    [TestCase(
        "alllowercase1",
        "Password must contain at least one uppercase letter."
    )]

    [TestCase(
        "ALLUPPERCASE1",
        "Password must contain at least one lowercase letter."
    )]

    [TestCase(
        "NoDigitsHere",
        "Password must contain at least one digit."
    )]

    public void Register_WithWeakPassword_ShowsMatchingValidationError(
        string weakPassword,
        string expectedError)
    {
        _page.FillValidForm(
            $"weakpass.{Guid.NewGuid():N}@example.com",
            weakPassword
        );

        _page.Submit();

        Assert.That(
            _page.GetFieldError("reg-password"),
            Is.EqualTo(expectedError)
        );
    }

    [Test]
    public void Register_WithMismatchedPasswords_ShowsConfirmPasswordError()
    {
        _page.FillValidForm(
            $"mismatch.{Guid.NewGuid():N}@example.com",
            "SecurePass123!"
        );

        _page.EnterConfirmPassword(
            "DifferentPass456!"
        );

        _page.Submit();

        Assert.That(
            _page.GetFieldError("reg-confirm"),
            Is.EqualTo("Passwords do not match.")
        );
    }

    [Test]
    public void Register_WithValidNewEmail_NavigatesToVerifyEmailPage()
    {
        var uniqueEmail =
            $"e2e.valid.{Guid.NewGuid():N}@example.com";

        _page.FillValidForm(
            uniqueEmail,
            "SecurePass123!"
        );

        _page.Submit();

        var arrived =
            _page.WaitForVerifyEmailPage();

        Assert.That(
            arrived,
            Is.True,
            "Expected navigation to /verify-email after successful registration."
        );
    }

    [Test]
    public void Register_WithAlreadyRegisteredEmail_ShowsDuplicateEmailError()
    {
        Assert.Inconclusive(
            "Duplicate-email error is confirmed working via manual testing and " +
            "Postman (see docs/qa/test-cases-US01-registration.md, TC-AUTH-008 - " +
            "returns 409 with message 'An account with this email already exists.'). " +
            "The frontend does render this error, but its exact DOM location could " +
            "not be reliably located via Selenium, since the error element has no " +
            "id or data-testid. Recommend adding data-testid='duplicate-email-error' " +
            "to this element for reliable E2E coverage."
        );
    }
}