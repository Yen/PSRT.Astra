using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public class PatchCache
    {
        private SQLiteConnection _Connection;

        private PatchCache(SQLiteConnection connection)
        {
            _Connection = connection;
        }

        public static async Task<PatchCache> CreateAsync(InstallConfiguration configuration)
        {
            var connection = new SQLiteConnection($"Data Source={configuration.PatchCacheDatabase};Version=3");
            await connection.OpenAsync();

            const string tableString = @"create table if not exists PatchInfo (
                Id integer not null primary key,
                Name text not null unique on conflict replace,
                Hash text not null,
                LastWriteTime integer not null
            );";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = tableString;
                await command.ExecuteNonQueryAsync();
            }

            return new PatchCache(connection);
        }

        public async Task<Dictionary<string, PatchCacheEntry>> SelectAllAsync()
        {
            var entries = new Dictionary<string, PatchCacheEntry>();

            using (var command = _Connection.CreateCommand())
            {
                command.CommandText = "select Name, Hash, LastWriteTime from PatchInfo";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var name = reader.GetString(0);
                        var hash = reader.GetString(1);
                        var lastWriteTime = reader.GetInt64(2);

                        entries[name] = new PatchCacheEntry()
                        {
                            Name = name,
                            Hash = hash,
                            LastWriteTime = lastWriteTime
                        };
                    }
                }
            }

            return entries;
        }

        public async Task InsertUnderTransactionAsync(IEnumerable<PatchCacheEntry> entries)
        {
            using (var command = _Connection.CreateCommand())
            using (var transaction = _Connection.BeginTransaction())
            {
                command.CommandText = "insert into PatchInfo (Name, Hash, LastWriteTime) values (@Name, @Hash, @LastWriteTime)";

                foreach (var e in entries)
                {
                    command.Parameters.AddWithValue("@Name", e.Name);
                    command.Parameters.AddWithValue("@Hash", e.Hash);
                    command.Parameters.AddWithValue("@LastWriteTime", e.LastWriteTime);

                    await command.ExecuteNonQueryAsync();
                }

                await Task.Run(() => transaction.Commit());
            }
        }
    }
}
