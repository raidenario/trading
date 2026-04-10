namespace Exchange.Trading.Domain.Tests;

public sealed class InstrumentRuntimeMigrationTests
{
    [Fact]
    public void Runtime_migration_defines_instrument_rule_tables()
    {
        var migrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "infra", "postgres", "003_instrument_runtime_model.sql"));
        var migration = File.ReadAllText(migrationPath);

        Assert.Contains("CREATE TABLE instrument_trading_rules", migration);
        Assert.Contains("CREATE TABLE instrument_market_config", migration);
        Assert.Contains("CREATE TABLE instrument_status", migration);
        Assert.Contains("INSERT INTO instrument_trading_rules", migration);
        Assert.Contains("INSERT INTO instrument_market_config", migration);
        Assert.Contains("INSERT INTO instrument_status", migration);
    }
}
