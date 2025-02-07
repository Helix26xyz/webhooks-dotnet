using Microsoft.Data.SqlClient;

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
            if (!await DatabaseExistsAsync())
            {
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
                return result != null;
            }
        }

        private async Task CreateDatabaseAsync()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.InitialCatalog;

            builder.InitialCatalog = "master";

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";

                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task ApplyMigrationsAsync()
        {
            var sqlFiles = Directory.GetFiles(_migrationsFolder, "*.sql");

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                foreach (var file in sqlFiles)
                {
                    var commandText = await File.ReadAllTextAsync(file);

                    using (var command = new SqlCommand(commandText, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}
