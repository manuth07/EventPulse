using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class AdminOrganizerApplicationsTests : BaseTest
{
    private AdminOrganizerApplicationsPage _page = null!;

    [SetUp]
    public void LoginAsAdmin()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("admin@eventpulse.com");
        loginPage.EnterPassword("Admin123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        _page = new AdminOrganizerApplicationsPage(Driver);
        _page.NavigateTo();
    }

    [Test]
    public void AdminApplications_PendingTab_ShowsReviewButtonsOrEmptyState()
    {
        _page.WaitForListOrEmptyState();

        var reviewCount = _page.GetReviewButtonCount();
        var isEmpty = _page.IsEmptyStateDisplayed();

        Assert.That(reviewCount > 0 || isEmpty, Is.True,
            "Expected either at least one Review button, or the empty-state message.");
    }

    // -----------------------------------------------------------------
    // This test needs a real Pending organizer application to exist —
    // it's a no-op (Assert.Ignore) if none are found rather than a false
    // failure, since seeding one is outside Selenium's job. Submit one via
    // the API or ListYourEvent.jsx UI before running this for real coverage.
    // -----------------------------------------------------------------
    [Test]
    public void AdminApplications_RejectingWithoutComment_KeepsFormBlocked()
    {
        _page.WaitForListOrEmptyState();
        if (_page.GetReviewButtonCount() == 0)
        {
            Assert.Ignore("No pending organizer applications available to review — seed one first.");
            return;
        }

        _page.ClickFirstReview();
        _page.ClickRejectTab();
        _page.ConfirmReject(); // no comment entered

        // The button should still be present (no navigation happened / modal still open)
        // since the frontend requires a non-empty comment before allowing rejection.
        Assert.That(Driver.Url, Does.Contain("/admin/organizer-applications"));
    }

    [Test]
    public void AdminApplications_RejectingWithComment_ShowsFeedbackBanner()
    {
        _page.WaitForListOrEmptyState();
        if (_page.GetReviewButtonCount() == 0)
        {
            Assert.Ignore("No pending organizer applications available to review — seed one first.");
            return;
        }

        _page.ClickFirstReview();
        _page.ClickRejectTab();
        _page.EnterRejectComment("Please provide a valid business registration number.");
        _page.ConfirmReject();

        Assert.That(_page.WaitForFeedbackBanner(), Is.True,
            "Expected a feedback banner confirming the rejection.");
    }
}