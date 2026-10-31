using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace quillbridge.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProspects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prospects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProspectId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Employees = table.Column<int>(type: "int", nullable: false),
                    Hq = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InvoiceProcess = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FitTier = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    InterestScore = table.Column<int>(type: "int", nullable: true),
                    LeadSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastTouch = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTouchType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastTouchNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SdrId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PhoneType = table.Column<int>(type: "int", nullable: false),
                    DoNotCall = table.Column<bool>(type: "bit", nullable: false),
                    DoNotCallReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prospects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prospects_Users_SdrId",
                        column: x => x.SdrId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_ProspectId",
                table: "Prospects",
                column: "ProspectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prospects_SdrId",
                table: "Prospects",
                column: "SdrId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prospects");
        }
    }
}
