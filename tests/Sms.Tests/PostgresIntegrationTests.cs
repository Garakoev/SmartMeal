using Npgsql;
using Sms.ConsoleApp;
using Sms.DemoServer;

namespace Sms.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES")))
            Skip = "Задайте SMS_TEST_POSTGRES для теста с реальным PostgreSQL и правом CREATEDB.";
    }
}

public sealed class PostgresIntegrationTests
{
    [PostgresFact]
    public async Task CreatesDatabaseAndTableAndUpsertsWithoutDuplicates()
    {
        var adminString = Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES")!;
        var builder = new NpgsqlConnectionStringBuilder(adminString);
        var maintenance = builder.Database ?? "postgres";
        builder.Database = "sms_it_" + Guid.NewGuid().ToString("N");
        builder.Pooling = false;
        var name = builder.Database;
        var repository = new PostgresMenuRepository(builder.ConnectionString, maintenance);
        try
        {
            await repository.InitializeAsync(default);
            await repository.InitializeAsync(default);
            await repository.SaveAsync(DemoMenu.Dishes, default);
            await repository.SaveAsync([DemoMenu.Dishes[0] with { Price = 75.25m }], default);
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var count = new NpgsqlCommand("SELECT count(*) FROM menu_items", connection);
            Assert.Equal(2L, await count.ExecuteScalarAsync());
            await using var price = new NpgsqlCommand("SELECT price FROM menu_items WHERE id = '5979224'", connection);
            Assert.Equal(75.25m, await price.ExecuteScalarAsync());
            await using var barcodes = new NpgsqlCommand("SELECT barcodes FROM menu_items WHERE id = '5979224'", connection);
            Assert.Equal(new[] { "57890975627974236429" }, (string[])(await barcodes.ExecuteScalarAsync())!);
        }
        finally
        {
            // Only the uniquely named database created by this test is removed.
            await using var admin = new NpgsqlConnection(adminString);
            await admin.OpenAsync();
            var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quoted} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
