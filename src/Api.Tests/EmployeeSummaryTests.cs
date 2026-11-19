using System.Net;
using System.Net.Http.Json;
using Api.Data;
using Api.Models;
using Api.Services;

namespace Api.Tests;

/// <summary>Days taken this year (the summary card) count approved leave only.</summary>
public class EmployeeSummaryTests
{
    /// <summary>Gives employee 9 (no seed leave) one Mon–Tue request this year (2 working days) and returns days taken.</summary>
    static async Task<decimal?> TakenWithOneRequest(LeaveStatus status)
    {
        using var db = TestDb.Create();
        var monday = Seed.MondayOnOrAfter(new DateOnly(DateTime.UtcNow.Year, 6, 1));
        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = 9, StartDate = monday, EndDate = monday.AddDays(1), Status = status, Reason = "Test" });
        await db.SaveChangesAsync();
        return await new LeaveService(db).DaysTakenThisYearAsync(9);
    }

    [Fact]
    public async Task Approved_leave_counts_as_taken() =>
        Assert.Equal(2m, await TakenWithOneRequest(LeaveStatus.Approved));

    [Fact]
    public async Task Pending_leave_is_not_taken() =>
        Assert.Equal(0m, await TakenWithOneRequest(LeaveStatus.Pending));

    [Fact]
    public async Task Rejected_leave_is_not_taken() =>
        Assert.Equal(0m, await TakenWithOneRequest(LeaveStatus.Rejected));
}

/// <summary>GET /employees/{id}/summary over HTTP: same access rules and 404 as /balance.</summary>
public class EmployeeSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Employee_sees_their_own_summary()
    {
        var farah = await api.SignedInAs(People.Farah);
        var res = await farah.GetAsync("/employees/6/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SummaryRow>();
        Assert.Equal(6, body!.EmployeeId);
        Assert.Equal(3m, body.DaysTakenThisYear); // seed: 6 has 3 approved days
    }

    [Fact]
    public async Task Employee_cannot_see_someone_elses_summary()
    {
        var farah = await api.SignedInAs(People.Farah);
        Assert.Equal(HttpStatusCode.Forbidden, (await farah.GetAsync("/employees/7/summary")).StatusCode);
    }

    [Fact]
    public async Task Unknown_employee_summary_is_404()
    {
        var hr = await api.SignedInAs(People.Dina); // HR may see everyone, so only the id is wrong
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync("/employees/999/summary")).StatusCode);
    }

    record SummaryRow(int EmployeeId, decimal DaysTakenThisYear);
}
