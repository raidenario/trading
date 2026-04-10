namespace Exchange.Trading.Domain.Tests;

public sealed class B3SchemaMigrationTests
{
    [Fact]
    public void Migration_script_adds_b3_tables_constraints_and_backfill()
    {
        var migration = ReadMigration("002_b3_core_model.sql");

        Assert.Contains("CREATE TABLE instruments", migration);
        Assert.Contains("CREATE TABLE participants", migration);
        Assert.Contains("CREATE TABLE trading_accounts", migration);
        Assert.Contains("CREATE TABLE trade_executions", migration);
        Assert.Contains("CREATE TABLE trade_allocations", migration);
        Assert.Contains("CREATE TABLE positions", migration);
        Assert.Contains("CREATE TABLE settlement_obligations", migration);
        Assert.Contains("CREATE UNIQUE INDEX ux_instruments_symbol", migration);
        Assert.Contains("ALTER TABLE orders ADD COLUMN instrument_id UUID", migration);
        Assert.Contains("ALTER TABLE orders ADD COLUMN trading_account_id UUID", migration);
        Assert.Contains("REFERENCES instruments(instrument_id)", migration);
        Assert.Contains("REFERENCES trading_accounts(trading_account_id)", migration);
        Assert.Contains("CREATE INDEX idx_positions_trading_account_instrument_date", migration);
        Assert.Contains("INSERT INTO instruments", migration);
        Assert.Contains("INSERT INTO trading_accounts", migration);
    }

    [Fact]
    public void Initial_schema_remains_compatible_with_existing_records()
    {
        var initialSchema = ReadMigration("001_init.sql");

        Assert.Contains("CREATE TABLE accounts", initialSchema);
        Assert.Contains("CREATE TABLE balances", initialSchema);
        Assert.Contains("CREATE TABLE orders", initialSchema);
        Assert.Contains("CREATE TABLE trades", initialSchema);
        Assert.Contains("CREATE TABLE ledger_entries", initialSchema);
        Assert.Contains("INSERT INTO accounts", initialSchema);
        Assert.Contains("INSERT INTO balances", initialSchema);
    }

    private static string ReadMigration(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "infra",
            "postgres",
            fileName));

        return File.ReadAllText(path);
    }
}
