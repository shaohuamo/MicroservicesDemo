using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductsMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProductValueRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "public"."Products"
                        WHERE "UnitPrice" IS NULL OR "QuantityInStock" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Products contains NULL UnitPrice or QuantityInStock values. Clean up the data before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                schema: "public",
                table: "Products",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "numeric(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "QuantityInStock",
                schema: "public",
                table: "Products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_QuantityInStock_Range",
                schema: "public",
                table: "Products",
                sql: """
                    "QuantityInStock" >= 0 AND "QuantityInStock" <= 1000000
                    """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_UnitPrice_Range",
                schema: "public",
                table: "Products",
                sql: """
                    "UnitPrice" >= 0.01 AND "UnitPrice" <= 99999999.99
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_QuantityInStock_Range",
                schema: "public",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_UnitPrice_Range",
                schema: "public",
                table: "Products");

            migrationBuilder.AlterColumn<double>(
                name: "UnitPrice",
                schema: "public",
                table: "Products",
                type: "numeric(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");

            migrationBuilder.AlterColumn<int>(
                name: "QuantityInStock",
                schema: "public",
                table: "Products",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
