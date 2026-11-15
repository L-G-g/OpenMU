using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketOrder",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerName = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    ExcellentOptionCount = table.Column<byte>(type: "smallint", nullable: false),
                    HasLuck = table.Column<bool>(type: "boolean", nullable: false),
                    HasSkill = table.Column<bool>(type: "boolean", nullable: false),
                    OptionLevel = table.Column<byte>(type: "smallint", nullable: false),
                    IsAncient = table.Column<bool>(type: "boolean", nullable: false),
                    SocketCount = table.Column<byte>(type: "smallint", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RemainingQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketOrder_Account_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "data",
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketTrade",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "text", nullable: false),
                    BuyerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerName = table.Column<string>(type: "text", nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    ExcellentOptionCount = table.Column<byte>(type: "smallint", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Commission = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketTrade", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrder_AccountId",
                schema: "data",
                table: "MarketOrder",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrder_State_Kind",
                schema: "data",
                table: "MarketOrder",
                columns: new[] { "State", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrade_CreatedAt",
                schema: "data",
                table: "MarketTrade",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarketTrade_ItemGroup_ItemNumber",
                schema: "data",
                table: "MarketTrade",
                columns: new[] { "ItemGroup", "ItemNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketOrder",
                schema: "data");

            migrationBuilder.DropTable(
                name: "MarketTrade",
                schema: "data");
        }
    }
}
