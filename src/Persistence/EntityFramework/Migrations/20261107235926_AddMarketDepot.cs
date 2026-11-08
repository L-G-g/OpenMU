using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketDepot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MarketDepotId",
                schema: "data",
                table: "Account",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Account_MarketDepotId",
                schema: "data",
                table: "Account",
                column: "MarketDepotId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Account_ItemStorage_MarketDepotId",
                schema: "data",
                table: "Account",
                column: "MarketDepotId",
                principalSchema: "data",
                principalTable: "ItemStorage",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Account_ItemStorage_MarketDepotId",
                schema: "data",
                table: "Account");

            migrationBuilder.DropIndex(
                name: "IX_Account_MarketDepotId",
                schema: "data",
                table: "Account");

            migrationBuilder.DropColumn(
                name: "MarketDepotId",
                schema: "data",
                table: "Account");
        }
    }
}
