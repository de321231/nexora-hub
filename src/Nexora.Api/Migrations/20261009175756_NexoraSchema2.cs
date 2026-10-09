using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Api.Migrations
{
    /// <inheritdoc />
    public partial class NexoraSchema2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nexora");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Products",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "Licenses",
                newName: "Licenses",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "Downloads",
                newName: "Downloads",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "Devices",
                newName: "Devices",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "AuditLogs",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AppVersions",
                newName: "AppVersions",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "AppDefinitions",
                newName: "AppDefinitions",
                newSchema: "nexora");

            migrationBuilder.RenameTable(
                name: "Activations",
                newName: "Activations",
                newSchema: "nexora");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Products",
                schema: "nexora",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "Licenses",
                schema: "nexora",
                newName: "Licenses");

            migrationBuilder.RenameTable(
                name: "Downloads",
                schema: "nexora",
                newName: "Downloads");

            migrationBuilder.RenameTable(
                name: "Devices",
                schema: "nexora",
                newName: "Devices");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                schema: "nexora",
                newName: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "nexora",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "nexora",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "nexora",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "nexora",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "nexora",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "nexora",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "nexora",
                newName: "AspNetRoleClaims");

            migrationBuilder.RenameTable(
                name: "AppVersions",
                schema: "nexora",
                newName: "AppVersions");

            migrationBuilder.RenameTable(
                name: "AppDefinitions",
                schema: "nexora",
                newName: "AppDefinitions");

            migrationBuilder.RenameTable(
                name: "Activations",
                schema: "nexora",
                newName: "Activations");
        }
    }
}
