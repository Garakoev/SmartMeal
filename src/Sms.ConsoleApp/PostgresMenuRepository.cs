using System.Text;
using Npgsql;
using NpgsqlTypes;
using Sms.Client;

namespace Sms.ConsoleApp;

public interface IMenuRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task SaveAsync(IReadOnlyCollection<Dish> dishes, CancellationToken cancellationToken);
}

public sealed class PostgresMenuRepository(string connectionString, string maintenanceDatabase = "postgres") : IMenuRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) || Encoding.UTF8.GetByteCount(builder.Database) > 63)
            throw new ArgumentException("Задайте имя Database длиной не более 63 байт UTF-8.");

        try
        {
            await using var existing = new NpgsqlConnection(connectionString);
            await existing.OpenAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            var database = builder.Database;
            builder.Database = maintenanceDatabase;
            await using var admin = new NpgsqlConnection(builder.ConnectionString);
            await admin.OpenAsync(cancellationToken);
            // CREATE DATABASE cannot run in a transaction; identifiers cannot be SQL parameters.
            // Quoting protects arbitrary valid names rather than concatenating unchecked SQL.
            var quotedName = new NpgsqlCommandBuilder().QuoteIdentifier(database);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {quotedName}", admin);
            try { await create.ExecuteNonQueryAsync(cancellationToken); }
            catch (PostgresException race) when (race.SqlState == PostgresErrorCodes.DuplicateDatabase) { }
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var table = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS public.menu_items (
                id text PRIMARY KEY,
                article text NOT NULL,
                name text NOT NULL,
                price numeric NOT NULL CHECK (price >= 0),
                is_weighted boolean NOT NULL,
                full_path text NOT NULL,
                barcodes text[] NOT NULL,
                updated_at timestamptz NOT NULL DEFAULT now()
            )
            """, connection);
        await table.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(IReadOnlyCollection<Dish> dishes, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var dish in dishes)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO public.menu_items (id, article, name, price, is_weighted, full_path, barcodes)
                VALUES (@id, @article, @name, @price, @weighted, @path, @barcodes)
                ON CONFLICT (id) DO UPDATE SET
                    article = EXCLUDED.article, name = EXCLUDED.name, price = EXCLUDED.price,
                    is_weighted = EXCLUDED.is_weighted, full_path = EXCLUDED.full_path,
                    barcodes = EXCLUDED.barcodes, updated_at = now()
                """, connection, transaction);
            command.Parameters.AddWithValue("id", dish.Id);
            command.Parameters.AddWithValue("article", dish.Article);
            command.Parameters.AddWithValue("name", dish.Name);
            command.Parameters.AddWithValue("price", dish.Price);
            command.Parameters.AddWithValue("weighted", dish.IsWeighted);
            command.Parameters.AddWithValue("path", dish.FullPath);
            command.Parameters.AddWithValue("barcodes", NpgsqlDbType.Array | NpgsqlDbType.Text, dish.Barcodes);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
