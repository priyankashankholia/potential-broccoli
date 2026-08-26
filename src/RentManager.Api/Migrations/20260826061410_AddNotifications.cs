using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Notifications",
                newName: "Type");

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RentId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Notifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RentId",
                table: "Notifications",
                column: "RentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Rents_RentId",
                table: "Notifications",
                column: "RentId",
                principalTable: "Rents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Rents_RentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Notifications",
                newName: "Title");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
