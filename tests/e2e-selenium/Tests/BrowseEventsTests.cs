
using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
[Category("SE3112-E2E")]
public class BrowseEventsTests : BaseTest
{
    private BrowseEventsPage _browseEventsPage = null!;

    [SetUp]
    public void TestSetup()
    {
        _browseEventsPage = new BrowseEventsPage(Driver);
        _browseEventsPage.NavigateToHome(Constants.BaseUrl);
        _browseEventsPage.WaitForPageToLoad();
    }

    [Test]
    public void BrowseEvents_PageDisplaysUpcomingEventsSection()
    {
        Assert.That(
            _browseEventsPage.IsUpcomingEventsHeadingDisplayed(),
            Is.True,
            "The Upcoming Events heading should be displayed."
        );
    }

    [Test]
    public void BrowseEvents_DisplaysEventCards_WhenEventsAreAvailable()
    {
        var eventCardCount = _browseEventsPage.GetEventCardCount();
        var isEmptyState = _browseEventsPage.IsEmptyStateDisplayed();

        Assert.That(
            eventCardCount > 0 || isEmptyState,
            Is.True,
            "Either published event cards or a legitimate empty state should be displayed."
        );
    }

    [Test]
    public void BrowseEvents_DisplaysViewDetailsLinks_WhenEventsAreAvailable()
    {
        var viewDetailsCount = _browseEventsPage.GetViewDetailsLinkCount();
        var isEmptyState = _browseEventsPage.IsEmptyStateDisplayed();

        if (viewDetailsCount == 0 && isEmptyState)
        {
            Assert.Pass("Verified legitimate empty state when no events are published.");
            return;
        }

        Assert.That(
            viewDetailsCount,
            Is.GreaterThan(0),
            "At least one View Details link should be displayed when events exist."
        );
    }

    [Test]
    public void BrowseEvents_ClickingViewDetails_NavigatesToEventDetails()
    {
        var viewDetailsCount = _browseEventsPage.GetViewDetailsLinkCount();

        if (viewDetailsCount == 0)
        {
            Assert.Ignore("Cannot test navigation because no published events are currently available in the environment.");
            return;
        }

        _browseEventsPage.ClickFirstEventDetails();

        Assert.That(
            _browseEventsPage.GetCurrentUrl(),
            Does.Match(@".*/events/[^/]+$"),
            "Clicking View Details should navigate to /events/{id}."
        );
    }
}

