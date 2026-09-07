using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class CreateEventTests : BaseTest
{
    private CreateEventPage _page = null!;
    private const string SamplePosterPath = @"TestAssets\sample-poster.jpg";
    private const string SampleCoverPath = @"TestAssets\sample-cover.jpg";

    // NOTE: All tests here need a logged-in Organizer session first — this is
    // deliberately left as a manual login step since there's no shared
    // authenticated-navigation helper yet. Consider adding one (e.g. a
    // LoginPage.LoginAndReturnToken helper) if this pattern repeats across
    // more test classes.
    [SetUp]
    public void LoginAsOrganizer()
    {
        var loginPage = new LoginPage(Driver);
        loginPage.NavigateTo();
        loginPage.EnterEmail("organizer@eventpulse.dev");
        loginPage.EnterPassword("Organizer123!");
        loginPage.Submit();
        loginPage.WaitForRedirectAwayFromLogin();

        _page = new CreateEventPage(Driver);
        _page.NavigateTo();
    }

    [Test]
    public void CreateEvent_SubmittingWithoutPoster_ShowsPosterRequiredError()
    {
        _page.FillRequiredFields("Test Event", "A test description", "Test Venue",
            DateTime.UtcNow.AddDays(10), "1000");
        _page.Submit();

        Assert.That(_page.GetErrorMessage(), Does.Contain("poster"));
    }

    [Test]
    public void CreateEvent_SubmittingWithoutCover_ShowsCoverRequiredError()
    {
        _page.FillRequiredFields("Test Event", "A test description", "Test Venue",
            DateTime.UtcNow.AddDays(10), "1000");
        _page.UploadPoster(Path.GetFullPath(SamplePosterPath));
        _page.Submit();

        Assert.That(_page.GetErrorMessage(), Does.Contain("cover"));
    }

    [Test]
    public void CreateEvent_ValidSubmission_ShowsSuccessAndRedirects()
    {
        var uniqueTitle = $"E2E Created Event {Guid.NewGuid():N}";
        _page.FillRequiredFields(uniqueTitle, "A valid E2E test description.", "Test Venue, Colombo",
            DateTime.UtcNow.AddDays(15), "1500");
        _page.UploadPoster(Path.GetFullPath(SamplePosterPath));
        _page.UploadCover(Path.GetFullPath(SampleCoverPath));
        _page.Submit();

        Assert.That(_page.WaitForSuccessBanner(), Is.True, "Expected the success banner after valid submission.");
        Assert.That(_page.WaitForRedirectToOrganizerDashboard(timeoutSeconds: 5), Is.True,
            "Expected redirect to /organizer within ~2s (setTimeout) after success.");
    }
}