using CourseInquiryDashboard.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CourseInquiryDashboard.Tests.Integration;

/// <summary>
/// Deliberately failing migration that lives only in the test assembly (IT-DATA-003). A host whose
/// migrations assembly is the test assembly discovers it after the real migration set, so startup
/// fails on top of a real, already-migrated schema instead of an empty store. The production
/// migration set is never modified.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration(Id)]
public sealed class TestOnlyFatalMigration : Migration
{
    public const string Id = "20991231235959_TestOnlyFatalMigration";

    public const string SentinelMessage =
        "DeliberateMigrationFailure";

    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql($"SELECT * FROM \"{SentinelMessage}\";");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
