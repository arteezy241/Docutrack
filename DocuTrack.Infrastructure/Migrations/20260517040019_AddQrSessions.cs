using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQrSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QrSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    IsScanned = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScannedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    JwtToken = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrSessions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QrSessions");
        }
    }
}
