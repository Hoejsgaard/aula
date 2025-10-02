using System;
using System.IO;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "postgresql://postgres.ddepyewmoxshsjjdlfpl:Mærke878@aws-0-eu-central-1.pooler.supabase.co:6543/postgres";
        var sqlFile = "/mnt/d/git/aula/reset_week40.sql";

        var sql = await File.ReadAllTextAsync(sqlFile);

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();

        Console.WriteLine("Database reset completed successfully!");
        Console.WriteLine($"Result: {result}");
    }
}