using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stackline.API.Data.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCategoryNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_Categories_Root_Name"
                ON "Categories" (lower(btrim("Name")))
                WHERE NOT "IsDeleted"
                  AND "ParentCategoryId" IS NULL;
                """);

                    migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_Categories_Parent_Name"
                ON "Categories" (
                    "ParentCategoryId",
                    lower(btrim("Name"))
                )
                WHERE NOT "IsDeleted"
                  AND "ParentCategoryId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "UX_Categories_Root_Name";
                """);

                    migrationBuilder.Sql("""
                DROP INDEX "UX_Categories_Parent_Name";
                """);
        }
    }
}
