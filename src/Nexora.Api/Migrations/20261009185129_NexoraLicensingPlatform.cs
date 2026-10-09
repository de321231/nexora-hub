using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Api.Migrations
{
    /// <inheritdoc />
    public partial class NexoraLicensingPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Licenses_Products_ProductId",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_KeyHash",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "KeyHash",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "nexora",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "nexora",
                table: "Products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "AdminNotes",
                schema: "nexora",
                table: "Licenses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxInputTokensPerDay",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxInputTokensPerMonth",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMessagesPerDay",
                schema: "nexora",
                table: "Licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMessagesPerMonth",
                schema: "nexora",
                table: "Licenses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxOutputTokensPerDay",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxOutputTokensPerMonth",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxTokensPerRequest",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxTotalTokensPerDay",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxTotalTokensPerMonth",
                schema: "nexora",
                table: "Licenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "nexora",
                table: "Licenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                schema: "nexora",
                table: "Licenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "nexora",
                table: "Licenses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DeviceIdentifier",
                schema: "nexora",
                table: "Devices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Platform",
                schema: "nexora",
                table: "Devices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "nexora",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                schema: "nexora",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiModels",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxContextTokens = table.Column<int>(type: "integer", nullable: true),
                    InputCostPer1M = table.Column<decimal>(type: "numeric", nullable: true),
                    OutputCostPer1M = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseKeys",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    RedeemedByUserId = table.Column<string>(type: "text", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseKeys_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "nexora",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageEvents",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    AiModelId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    TokensEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    CostUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsagePeriods",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLicenses",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LicenseKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLicenses_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "nexora",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LicenseModels",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseModels_AiModels_AiModelId",
                        column: x => x.AiModelId,
                        principalSchema: "nexora",
                        principalTable: "AiModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LicenseModels_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "nexora",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                schema: "nexora",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "nexora",
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                schema: "nexora",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_Status",
                schema: "nexora",
                table: "Licenses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceIdentifier",
                schema: "nexora",
                table: "Devices",
                column: "DeviceIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_ModelId",
                schema: "nexora",
                table: "AiModels",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserId",
                schema: "nexora",
                table: "ChatConversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId",
                schema: "nexora",
                table: "ChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseKeys_KeyHash",
                schema: "nexora",
                table: "LicenseKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseKeys_LicenseId",
                schema: "nexora",
                table: "LicenseKeys",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseKeys_Status",
                schema: "nexora",
                table: "LicenseKeys",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseModels_AiModelId",
                schema: "nexora",
                table: "LicenseModels",
                column: "AiModelId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseModels_LicenseId_AiModelId",
                schema: "nexora",
                table: "LicenseModels",
                columns: new[] { "LicenseId", "AiModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "nexora",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                schema: "nexora",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_CreatedAtUtc",
                schema: "nexora",
                table: "UsageEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_LicenseId",
                schema: "nexora",
                table: "UsageEvents",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageEvents_UserId",
                schema: "nexora",
                table: "UsageEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsagePeriods_UserId_LicenseId_PeriodType_PeriodStartUtc",
                schema: "nexora",
                table: "UsagePeriods",
                columns: new[] { "UserId", "LicenseId", "PeriodType", "PeriodStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_LicenseId",
                schema: "nexora",
                table: "UserLicenses",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_UserId",
                schema: "nexora",
                table: "UserLicenses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLicenses_UserId_LicenseId",
                schema: "nexora",
                table: "UserLicenses",
                columns: new[] { "UserId", "LicenseId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Licenses_Products_ProductId",
                schema: "nexora",
                table: "Licenses",
                column: "ProductId",
                principalSchema: "nexora",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Licenses_Products_ProductId",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropTable(
                name: "ChatMessages",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "LicenseKeys",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "LicenseModels",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "UsageEvents",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "UsagePeriods",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "UserLicenses",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "ChatConversations",
                schema: "nexora");

            migrationBuilder.DropTable(
                name: "AiModels",
                schema: "nexora");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                schema: "nexora",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Licenses_Status",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropIndex(
                name: "IX_Devices_DeviceIdentifier",
                schema: "nexora",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "nexora",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "nexora",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxInputTokensPerDay",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxInputTokensPerMonth",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxMessagesPerDay",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxMessagesPerMonth",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokensPerDay",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokensPerMonth",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxTokensPerRequest",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxTotalTokensPerDay",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "MaxTotalTokensPerMonth",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "StartsAt",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "nexora",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "DeviceIdentifier",
                schema: "nexora",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Platform",
                schema: "nexora",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "nexora",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginAtUtc",
                schema: "nexora",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "AdminNotes",
                schema: "nexora",
                table: "Licenses",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyHash",
                schema: "nexora",
                table: "Licenses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "nexora",
                table: "Licenses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_KeyHash",
                schema: "nexora",
                table: "Licenses",
                column: "KeyHash");

            migrationBuilder.AddForeignKey(
                name: "FK_Licenses_Products_ProductId",
                schema: "nexora",
                table: "Licenses",
                column: "ProductId",
                principalSchema: "nexora",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
