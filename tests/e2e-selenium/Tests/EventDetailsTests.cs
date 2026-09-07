
using NUnit.Framework;
using EventPulse.E2E.Tests.Pages;

namespace EventPulse.E2E.Tests.Tests;

[TestFixture]
public class EventDetailsTests : BaseTest
{
    private BrowseEventsPage _browseEventsPage = null!;
    private EventDetailsPage _eventDetailsPage = null!;

    [SetUp]
    public void TestSetup()
    {
        _browseEventsPage = new BrowseEventsPage(Driver);
        _eventDetailsPage = new EventDetailsPage(Driver);

        // Start from the real browse-events page.
        _browseEventsPage.NavigateToHome(Constants.BaseUrl);
        _browseEventsPage.WaitForPageToLoad();

        var viewDetailsCount = _browseEventsPage.GetViewDetailsLinkCount();

        Assert.That(
            viewDetailsCount,
            Is.GreaterThan(0),
            "No published/approved events are available for the event-details test."
        );

        // Use a real event ID from the UI instead of hardcoding a GUID.
        _browseEventsPage.ClickFirstEventDetails();

        _eventDetailsPage.WaitForPageToLoad();
    }

    [Test]
    public void EventDetails_DisplaysEventTitle()
    {
        Assert.That(
            _eventDetailsPage.IsEventTitleDisplayed(),
            Is.True,
            "The event title should be displayed."
        );

        Assert.That(
            _eventDetailsPage.GetEventTitle(),
            Is.Not.Empty,
            "The event title should not be empty."
        );
    }

    [Test]
    public void EventDetails_DisplaysDateAndTime()
    {
        Assert.That(
            _eventDetailsPage.IsDateAndTimeDisplayed(),
            Is.True,
            "The Date & Time information should be displayed."
        );
    }

    [Test]
    public void EventDetails_DisplaysVenue()
    {
        Assert.That(
            _eventDetailsPage.IsVenueDisplayed(),
            Is.True,
            "The Venue information should be displayed."
        );
    }

    [Test]
    public void EventDetails_DisplaysDescription()
    {
        Assert.That(
            _eventDetailsPage.IsAboutEventDisplayed(),
            Is.True,
            "The About this Event heading should be displayed."
        );

        Assert.That(
            _eventDetailsPage.IsDescriptionDisplayed(),
            Is.True,
            "The event description should be displayed."
        );
    }

    [Test]
    public void EventDetails_DisplaysTicketInformationAndPrice()
    {
        Assert.That(
            _eventDetailsPage.IsTicketInformationDisplayed(),
            Is.True,
            "The Ticket Information section should be displayed."
        );

        Assert.That(
            _eventDetailsPage.IsPriceDisplayed(),
            Is.True,
            "The starting price should be displayed."
        );

        Assert.That(
            _eventDetailsPage.GetPrice(),
            Is.Not.Empty,
            "The event price should not be empty."
        );
    }

    [Test]
    public void EventDetails_DisplaysBackToEventsLink()
    {
        Assert.That(
            _eventDetailsPage.IsBackToEventsDisplayed(),
            Is.True,
            "The Back to events link should be displayed."
        );
    }

    [Test]
    public void EventDetails_BackToEvents_ReturnsToHome()
    {
        _eventDetailsPage.ClickBackToEvents();

        Assert.That(
            _eventDetailsPage.GetCurrentUrl().TrimEnd('/'),
            Is.EqualTo(Constants.BaseUrl.TrimEnd('/')),
            "Back to events should return the user to the home page."
        );
    }
}
