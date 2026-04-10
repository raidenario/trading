using Exchange.Query.Api;

namespace Exchange.Trading.Domain.Tests;

public sealed class ProjectionSchemaSqlBuilderTests
{
    [Fact]
    public void SupportsExtendedOrders_requires_all_b3_columns()
    {
        var legacyColumns = new[]
        {
            "order_id",
            "account_id",
            "symbol",
            "side",
            "order_type",
            "time_in_force",
            "quantity",
            "limit_price",
            "filled_quantity",
            "remaining_quantity",
            "status",
            "rejection_reason",
            "client_order_id",
            "created_at",
            "updated_at"
        };

        var extendedColumns = legacyColumns.Concat(
        [
            "instrument_id",
            "trading_account_id",
            "source_system",
            "execution_instructions",
            "stop_price"
        ]).ToArray();

        Assert.False(ProjectionSchemaSqlBuilder.SupportsExtendedOrders(legacyColumns));
        Assert.True(ProjectionSchemaSqlBuilder.SupportsExtendedOrders(extendedColumns));
    }

    [Fact]
    public void BuildUpsertOrderSql_falls_back_to_legacy_orders_schema_when_needed()
    {
        var legacySql = ProjectionSchemaSqlBuilder.BuildUpsertOrderSql(new ProjectionSchemaCapabilities(false, false));
        var extendedSql = ProjectionSchemaSqlBuilder.BuildUpsertOrderSql(new ProjectionSchemaCapabilities(true, true));

        Assert.DoesNotContain("instrument_id", legacySql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("execution_instructions", legacySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("instrument_id", extendedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("execution_instructions", extendedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@execution_instructions::jsonb", extendedSql, StringComparison.OrdinalIgnoreCase);
    }
}
