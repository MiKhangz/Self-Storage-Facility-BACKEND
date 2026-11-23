using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Tests;

/// <summary>The test database must refuse what SQL Server refuses, or a too-long value only fails in production.</summary>
public class TestDbTests
{
    [Fact]
    public async Task Text_longer_than_its_column_is_refused_like_on_sql_server()
    {
        using var db = new TestDb();
        var facility = new Facility
        {
            Code = "F1", Name = "F1", Address = "a", District = "d", Phone = "1", OpeningHours = "07-21", Status = FacilityStatus.Active,
        };
        var user = new User
        {
            Role = new Role { RoleName = "FacilityStaff" }, FullName = "s", Email = "s@x.vn", PhoneNumber = "0900000000", PasswordHash = "x",
            Status = UserStatus.Active,
        };
        db.Db.StaffFacilities.Add(new StaffFacility { User = user, Facility = facility, AssignedDate = new DateOnly(2026, 11, 1), Timetable = new string('x', 201) });

        var save = () => db.Db.SaveChangesAsync();

        (await save.Should().ThrowAsync<DbUpdateException>()).WithMessage("StaffFacility.Timetable is 201 characters; the column takes 200.");
    }
}
