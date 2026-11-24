using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.UnitTests.Persistence;

/// <summary>Creates SQLite in-memory contexts for persistence tests.</summary>
internal static class SqliteTestContext
{
    /// <summary>Creates a context and initializes its tables from the current EF model.</summary>
    /// <returns>The open connection and context. Dispose both after the test.</returns>
    public static async Task<(SqliteConnection Connection, AppDbContext Context)> CreateAsync()
    {
        var connection = CreateOpenConnection();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return (connection, context);
    }

    /// <summary>Opens an in-memory SQLite database and registers functions used by SQL Server checks.</summary>
    /// <returns>An open SQLite connection.</returns>
    public static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateCollation("Latin1_General_100_CI_AS", (left, right) =>
            string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        connection.CreateFunction<string?, int>("LEN", value => value?.TrimEnd().Length ?? 0);
        connection.CreateFunction<string?, long>("DATALENGTH", value => value is null ? 0 : Encoding.Unicode.GetByteCount(value));
        connection.CreateFunction<string?, string>("LTRIM", value => value?.TrimStart() ?? string.Empty);
        connection.CreateFunction<string?, string>("RTRIM", value => value?.TrimEnd() ?? string.Empty);
        connection.Open();
        return connection;
    }
}
