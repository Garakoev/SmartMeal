using Sms.ConsoleApp;
using Sms.DemoServer;

namespace Sms.Tests;

public sealed class OrderParserTests
{
    [Theory]
    [InlineData("A1004292:1;A1004293:0.408")]
    [InlineData(" a1004292 : 1 ; A1004293 : 0,408 ;")]
    public void MapsArticlesToIdsAndAcceptsFractionalQuantities(string text)
    {
        var result = OrderParser.Parse(text, DemoMenu.Dishes);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Collection(result.Value!,
            i => { Assert.Equal("5979224", i.Id); Assert.Equal(1m, i.Quantity); },
            i => { Assert.Equal("9084246", i.Id); Assert.Equal(0.408m, i.Quantity); });
    }

    [Theory]
    [InlineData("")]
    [InlineData(";")]
    [InlineData("UNKNOWN:1")]
    [InlineData("5979224:1")]
    [InlineData("A1004292:0")]
    [InlineData("A1004292:-1")]
    [InlineData("A1004292:NaN")]
    [InlineData("A1004292:1e2")]
    [InlineData("A1004292:1:2")]
    [InlineData("A1004292:1;;A1004293:1")]
    [InlineData("A1004292:1.2,3")]
    [InlineData("A1004292:999999999999999999999999999999999")]
    public void RejectsInvalidInput(string text)
    {
        var result = OrderParser.Parse(text, DemoMenu.Dishes);
        Assert.False(result.Success);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void AddsRepeatedArticles()
    {
        var result = OrderParser.Parse("A1004293:0.408;a1004293:0.592", DemoMenu.Dishes);
        Assert.True(result.Success);
        Assert.Equal(1m, Assert.Single(result.Value!).Quantity);
    }

    [Fact]
    public void ReportsOverflowWhenAddingRepeatedArticles()
    {
        Assert.False(OrderParser.Parse($"A1004292:{decimal.MaxValue};A1004292:1", DemoMenu.Dishes).Success);
    }
}
