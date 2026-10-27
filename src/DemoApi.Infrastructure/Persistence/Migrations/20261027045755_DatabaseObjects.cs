using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoApi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// View, functions, the refresh-token procedure and app-role grants (data-model section 4.3).
    /// SQL files are versioned and never edited after merge; a change adds a v2 file and a new migration.
    /// </summary>
    public partial class DatabaseObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("Procedures/revoke_refresh_tokens.v1.sql"));
            migrationBuilder.Sql(SqlResource.Read("Views/v_order_summaries.v1.sql"));
            migrationBuilder.Sql(SqlResource.Read("Functions/fn_customer_order_history.v1.sql"));
            migrationBuilder.Sql(SqlResource.Read("Functions/fn_sales_report.v1.sql"));
            migrationBuilder.Sql(SqlResource.Read("Functions/fn_top_products.v1.sql"));
            migrationBuilder.Sql(SqlResource.Read("Grants/app_role_grants.v1.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_top_products(timestamptz, timestamptz, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_sales_report(timestamptz, timestamptz, text);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.fn_customer_order_history(uuid, int, int);");
            migrationBuilder.Sql("DROP VIEW IF EXISTS public.v_order_summaries;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS public.revoke_refresh_tokens(uuid, uuid, varchar, timestamptz, integer);");
        }
    }
}
