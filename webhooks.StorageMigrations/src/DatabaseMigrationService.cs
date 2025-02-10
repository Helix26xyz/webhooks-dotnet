using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Data;

namespace webhooks.StorageMigrations.src
{
    public class DatabaseMigrationService
    {
        private readonly string _connectionString;
        private readonly string _migrationsFolder;

        public DatabaseMigrationService(string connectionString, string migrationsFolder)
        {
            _connectionString = connectionString;
            _migrationsFolder = migrationsFolder;
        }

        public async Task EnsureDatabaseMigratedAsync()
        {
           Console.WriteLine("Checking if database exists");
            if (!await DatabaseExistsAsync())
            {
                Console.WriteLine("Creating database");
                await CreateDatabaseAsync();


            }

            await ApplyMigrationsAsync();
        }

        private async Task<bool> DatabaseExistsAsync()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.InitialCatalog;

            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"SELECT database_id FROM sys.databases WHERE Name = '{databaseName}'";

                var result = await command.ExecuteScalarAsync();
                if (result == null)
                {
                    Console.WriteLine("Database does not exist");
                } else
                {
                    Console.WriteLine("Database exists");
                }
                    return result != null;
            }
        }

        private async Task CreateDatabaseAsync()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.InitialCatalog;

            if (databaseName == null || databaseName == String.Empty)
            {
                databaseName = "webhooks";
            }

            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();

                }

                Console.WriteLine($"Creating database {databaseName}");
                var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";

                await command.ExecuteNonQueryAsync();

            }
            using (var connection = new SqlConnection(_connectionString))
            { 
            // run 10_schema.sql
            Console.WriteLine("Applying 10_schema.sql");
            var schemaFilePath = Path.Combine(_migrationsFolder, "10_schema.sql");
            var schemaCommandText = await File.ReadAllTextAsync(schemaFilePath);
            await RunSQLNonQueryCommandAsync(schemaCommandText, connection);
            await MarkMigrationCompleteAsync("10_schema.sql", connection); }
        }
        static private async Task RunSQLNonQueryCommandAsync(string commandText, SqlConnection connection )
        {

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();

                }
                using (var command = new SqlCommand(commandText, connection))
                {
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    Console.WriteLine($"Command executed successfully. Affected rows: {affectedRows}");
                }
            }
        }
        private async Task<SqlDataReader> RunSQLQueryReadAsync(string commandText, SqlConnection connection )
        {

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();

                }
                using (var command = new SqlCommand(commandText, connection))
                {
                    var res = await command.ExecuteReaderAsync();
                    return res;
                }
        }
        private async Task MarkMigrationCompleteAsync(string migrationName, SqlConnection connection )
        {

                var commandText = $"INSERT INTO [dbo].[Migrations] ([MigrationName], [Backend]) VALUES ('{migrationName}', 'CoreDatabase')";
                await RunSQLNonQueryCommandAsync(commandText, connection);
        }
        private async Task ApplyMigrationsAsync()
        {
            var sqlFiles = Directory.GetFiles(_migrationsFolder, "*.sql");

            // sort by name
            Array.Sort(sqlFiles);

            using (var connection = new SqlConnection(_connectionString))
            {
                // get current migration status
                var query = "SELECT [MigrationName] FROM [dbo].[Migrations] WHERE [Backend] = 'CoreDatabase'";
                var appliedMigrations = new List<string>();

                using (var reader = await RunSQLQueryReadAsync(query, connection))
                {
                    while (await reader.ReadAsync())
                    {
                        appliedMigrations.Add(reader.GetString(0));
                    }
                }

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                foreach (var file in sqlFiles)
                {
                    var fileName = Path.GetFileName(file);

                    // check if the migration has already been applied
                    if (appliedMigrations.Contains(fileName))
                    {
                        Console.WriteLine($"Skipping already applied migration: {fileName}");
                        continue;
                    }

                    var commandText = await File.ReadAllTextAsync(file);
                    await RunSQLNonQueryCommandAsync(commandText, connection);
                    await MarkMigrationCompleteAsync(fileName, connection);
                }
            }
        }
    }
}
