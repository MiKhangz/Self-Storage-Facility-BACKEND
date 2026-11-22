using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using StorEase.Api.Controllers;
using StorEase.Application.Common;

namespace StorEase.Api.Tests;

public class HandoverRolesTests
{
    [Fact]
    public void Business_Ops_read_handovers_to_settle_a_deposit_but_never_write_them()
    {
        RolesOf(nameof(HandoversController.List)).Should().Contain(Roles.BusinessOperationsManager);
        foreach (var write in new[] { nameof(HandoversController.Create), nameof(HandoversController.Cancel), nameof(HandoversController.AddPhoto) })
            RolesOf(write).Should().BeEquivalentTo(Roles.FacilityStaff, Roles.FacilityManager);
    }

    private static string[] RolesOf(string action) =>
        typeof(HandoversController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
}
