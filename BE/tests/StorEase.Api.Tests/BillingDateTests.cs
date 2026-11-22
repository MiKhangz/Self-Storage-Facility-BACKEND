using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StorEase.Api.Controllers;
using StorEase.Application.Common;
using StorEase.Application.Features.Billing;

namespace StorEase.Api.Tests;

public class BillingDateTests
{
    private sealed class Billing : IBillingCycleService
    {
        public Task<Result<BillingRunDto>> RunAsync(DateOnly today, CancellationToken ct) =>
            Task.FromResult(Result<BillingRunDto>.Success(new BillingRunDto(today, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
    }

    private sealed class Host(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "StorEase";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Development", false, 120, 400)] // the defense laptop: one typo would lock customers out
    [InlineData("Development", false, -1, 400)]
    [InlineData("Production", true, 120, 400)]
    [InlineData("Development", true, 120, 200)] // the E2E tests
    [InlineData("Production", false, 0, 200)]
    [InlineData("Production", false, null, 200)]
    public async Task Another_date_needs_Development_and_the_switch(string environment, bool allowed, int? daysAhead, int status)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Billing:AllowOtherDates", allowed.ToString())]).Build();
        var date = daysAhead is { } days ? VietnamTime.Today.AddDays(days) : (DateOnly?)null;

        var result = await new BillingController(new Billing(), new Host(environment), config).Run(date, default);

        ((ObjectResult)result.Result!).StatusCode.Should().Be(status);
    }
}
