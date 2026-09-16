using EventPulse.EventService.Controllers;
using EventPulse.EventService.DTOs;
using EventPulse.EventService.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EventPulse.EventService.Tests;

public class EventCategoriesTests
{
    [Fact]
    public void All_ContainsExpectedCanonicalCategories()
    {
        var expected = new[]
        {
            "Music",
            "Sports",
            "Conference",
            "Workshop",
            "Festival",
            "Arts & Theatre",
            "Community",
            "Other"
        };

        Assert.Equal(expected.Length, EventCategories.All.Count);
        foreach (var category in expected)
        {
            Assert.Contains(category, EventCategories.All);
        }
    }

    [Theory]
    [InlineData("Music", true)]
    [InlineData("music", true)]
    [InlineData("MUSIC", true)]
    [InlineData("Sports", true)]
    [InlineData("Conference", true)]
    [InlineData("Workshop", true)]
    [InlineData("Festival", true)]
    [InlineData("Arts & Theatre", true)]
    [InlineData("Community", true)]
    [InlineData("Other", true)]
    [InlineData("Musical Concert", true)]
    [InlineData("musical concert", true)]
    [InlineData("Theatre / Performance", true)]
    [InlineData("InvalidCategory", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValid_ValidatesCorrectly(string? category, bool expected)
    {
        var result = EventCategories.IsValid(category);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Music", "Music")]
    [InlineData("music", "Music")]
    [InlineData("MUSIC", "Music")]
    [InlineData("Sports", "Sports")]
    [InlineData("Conference", "Conference")]
    [InlineData("Workshop", "Workshop")]
    [InlineData("Festival", "Festival")]
    [InlineData("Arts & Theatre", "Arts & Theatre")]
    [InlineData("Community", "Community")]
    [InlineData("Other", "Other")]
    [InlineData("Musical Concert", "Music")]
    [InlineData("musical concert", "Music")]
    [InlineData("Theatre / Performance", "Arts & Theatre")]
    [InlineData("InvalidCategory", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_NormalizesCorrectly(string? input, string? expected)
    {
        var result = EventCategories.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetCategories_Endpoint_ReturnsAllCanonicalCategoriesWithLabels()
    {
        var controller = new EventsController(null!, null!, null!, null!, null!, null!);

        var actionResult = controller.GetCategories();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var categories = Assert.IsAssignableFrom<IEnumerable<EventCategoryItemDto>>(okResult.Value).ToList();

        Assert.Equal(8, categories.Count);
        Assert.Equal("Music", categories[0].Value);
        Assert.Equal("Music", categories[0].Label);
        Assert.Equal("Arts & Theatre", categories[5].Value);
        Assert.Equal("Arts & Theatre", categories[5].Label);
    }
}
