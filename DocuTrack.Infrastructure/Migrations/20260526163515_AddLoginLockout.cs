using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutUntil",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetOtp",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetOtpExpiry",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId1",
                table: "RoutingEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollegeId",
                table: "Departments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserEmail = table.Column<string>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Colleges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colleges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutingTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    StepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingEvents_DocumentId1",
                table: "RoutingEvents",
                column: "DocumentId1");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CollegeId",
                table: "Departments",
                column: "CollegeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Colleges_CollegeId",
                table: "Departments",
                column: "CollegeId",
                principalTable: "Colleges",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RoutingEvents_Documents_DocumentId1",
                table: "RoutingEvents",
                column: "DocumentId1",
                principalTable: "Documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Colleges_CollegeId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_RoutingEvents_Documents_DocumentId1",
                table: "RoutingEvents");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Colleges");

            migrationBuilder.DropTable(
                name: "RoutingTemplates");

            migrationBuilder.DropIndex(
                name: "IX_RoutingEvents_DocumentId1",
                table: "RoutingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Departments_CollegeId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetOtp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetOtpExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DocumentId1",
                table: "RoutingEvents");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CollegeId",
                table: "Departments");
        }
    }
}
