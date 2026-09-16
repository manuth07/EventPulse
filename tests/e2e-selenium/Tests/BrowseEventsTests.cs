
using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
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

        Assert.That(
            eventCardCount,
            Is.GreaterThan(0),
            "At least one event card should be displayed when published events are available."
        );
    }

    [Test]
    public void BrowseEvents_DisplaysViewDetailsLinks_WhenEventsAreAvailable()
    {
        var viewDetailsCount = _browseEventsPage.GetViewDetailsLinkCount();

        Assert.That(
            viewDetailsCount,
            Is.GreaterThan(0),
            "At least one View Details link should be displayed."
        );
    }

    [Test]
    public void BrowseEvents_ClickingViewDetails_NavigatesToEventDetails()
    {
        var viewDetailsCount = _browseEventsPage.GetViewDetailsLinkCount();

        Assert.That(
            viewDetailsCount,
            Is.GreaterThan(0),
            "Cannot test navigation because no View Details links are available."
        );

        _browseEventsPage.ClickFirstEventDetails();

        Assert.That(
            _browseEventsPage.GetCurrentUrl(),
            Does.Match(@".*/events/[^/]+$"),
            "Clicking View Details should navigate to /events/{id}."
        );
    }
}

