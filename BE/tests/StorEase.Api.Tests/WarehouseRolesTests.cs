using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using StorEase.Api.Controllers;
using StorEase.Application.Common;

namespace StorEase.Api.Tests;

public class WarehouseRolesTests
{
    [Theory]
    [InlineData(nameof(ContractsController.Ending), Roles.FacilityManager)]
    [InlineData(nameof(ContractsController.Ending), Roles.BusinessOperationsManager)]
    [InlineData(nameof(ContractsController.MoveToCentralStorage), Roles.FacilityManager)]
    [InlineData(nameof(ContractsController.MoveToCentralStorage), Roles.BusinessOperationsManager)]
    public void Both_roles_can_move_goods_to_the_central_warehouse(string action, string role) =>
        typeof(ContractsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',')
            .Should().Contain(role);
}
