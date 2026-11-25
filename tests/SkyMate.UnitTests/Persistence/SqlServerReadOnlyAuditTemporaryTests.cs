using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SkyMate.UnitTests.Persistence;

public sealed class SqlServerReadOnlyAuditTemporaryTests
{
    [Fact]
    public async Task ReadConfiguredSqlServerSchemaWithoutWriting()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "sql-server-audit-temporary.txt");
        var output = new List<string>();
        var secretPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            "50e64041-7dd9-4a3a-9d3f-df25546666af",
            "secrets.json");

        if (!File.Exists(secretPath))
        {
            output.Add("DB_CHECK=NO_USER_SECRETS");
            await File.WriteAllLinesAsync(outputPath, output);
            return;
        }

        using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(secretPath));
        if (!secrets.RootElement.TryGetProperty("ConnectionStrings:DefaultConnection", out var secret) ||
            string.IsNullOrWhiteSpace(secret.GetString()))
        {
            output.Add("DB_CHECK=NO_CONNECTION_STRING");
            await File.WriteAllLinesAsync(outputPath, output);
            return;
        }

        var builder = new SqlConnectionStringBuilder(secret.GetString())
        {
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ConnectTimeout = 5,
            ApplicationName = "SkyMateFoundationAuditReadOnly"
        };
        output.Add($"DB_TARGET={builder.DataSource}/{builder.InitialCatalog}");

        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            output.Add("DB_CHECK=CONNECTED");

            await AddRowsAsync(connection, output, "SERVER",
                "SELECT @@SERVERNAME, DB_NAME();");

            var hasHistory = await ExecuteScalarAsync(connection,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END;");
            if (Convert.ToInt32(hasHistory) == 1)
            {
                await AddRowsAsync(connection, output, "MIGRATION",
                    "SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;");
            }
            else
            {
                output.Add("MIGRATIONS=NO_HISTORY_TABLE");
            }

            await AddRowsAsync(connection, output, "COLUMN",
                "SELECT t.name, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable " +
                "FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id " +
                "JOIN sys.types ty ON ty.user_type_id=c.user_type_id " +
                "WHERE t.name IN ('AiConversations','AiConversationMessages','KnowledgeBaseEntries','Trips','TripDays','Activities','AspNetUsers','UserPreferences','TripInterests') " +
                "ORDER BY t.name,c.column_id;");

            await AddRowsAsync(connection, output, "TABLE",
                "SELECT name FROM sys.tables ORDER BY name;");

            await AddRowsAsync(connection, output, "CHECK",
                "SELECT t.name, cc.name, cc.definition, cc.is_disabled, cc.is_not_trusted " +
                "FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id=cc.parent_object_id " +
                "ORDER BY t.name,cc.name;");

            await AddRowsAsync(connection, output, "FK",
                "SELECT fk.name, p.name, r.name, fk.delete_referential_action_desc " +
                "FROM sys.foreign_keys fk JOIN sys.tables p ON p.object_id=fk.parent_object_id " +
                "JOIN sys.tables r ON r.object_id=fk.referenced_object_id " +
                "WHERE p.name IN ('AiConversations','AiConversationMessages','Trips','TripDays','Activities','UserPreferences','TripInterests') " +
                "ORDER BY p.name,fk.name;");

            await AddRowsAsync(connection, output, "INDEX",
                "SELECT t.name, i.name, i.is_unique, i.filter_definition, c.name, ic.key_ordinal " +
                "FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id " +
                "JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id " +
                "JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id " +
                "WHERE t.name IN ('KnowledgeBaseEntries','AiConversations','AiConversationMessages','Trips','TripDays','Activities','UserPreferences','TripInterests') " +
                "AND i.name IS NOT NULL AND ic.is_included_column=0 ORDER BY t.name,i.name,ic.key_ordinal;");
        }
        catch (SqlException exception)
        {
            output.Add($"DB_CHECK=SQL_ERROR; NUMBER={exception.Number}; TYPE={exception.GetType().Name}");
        }
        catch (Exception exception)
        {
            output.Add($"DB_CHECK=ERROR; TYPE={exception.GetType().Name}");
        }

        await File.WriteAllLinesAsync(outputPath, output);
    }

    private static async Task<object?> ExecuteScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 10;
        return await command.ExecuteScalarAsync();
    }

    private static async Task AddRowsAsync(SqlConnection connection, ICollection<string> output, string prefix, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 10;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        while (await reader.ReadAsync())
        {
            var fields = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[index] = reader.IsDBNull(index) ? "<null>" : Convert.ToString(reader.GetValue(index)) ?? "<null>";
            }

            output.Add($"{prefix}=" + string.Join("|", fields));
        }
    }
}
