using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FirstDueDate",
                table: "Tenants",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstDueDate",
                table: "Tenants");
        }
    }
}
