using Microsoft.Extensions.Configuration;
using Supabase;
using MinUddannelse.Models;
using MinUddannelse.Configuration;

// Simple utility to reset week 40 reminders for testing
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Resetting week 40 reminders...");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("src/MinUddannelse/appsettings.json", optional: false)
            .Build();

        var config = new Config();
        configuration.Bind(config);

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            AutoRefreshToken = false
        };

        var client = new Supabase.Client(config.Supabase.Url, config.Supabase.ServiceRoleKey, options);
        await client.InitializeAsync();

        try
        {
            // Reset auto_reminders_extracted flag
            await client
                .From<PostedLetter>()
                .Where(x => x.ChildName == "Søren Johannes" && x.WeekNumber == 40 && x.Year == 2025)
                .Set(x => x.AutoRemindersExtracted, false)
                .Set(x => x.AutoRemindersLastUpdated, null)
                .Update();

            await client
                .From<PostedLetter>()
                .Where(x => x.ChildName == "Hans Martin" && x.WeekNumber == 40 && x.Year == 2025)
                .Set(x => x.AutoRemindersExtracted, false)
                .Set(x => x.AutoRemindersLastUpdated, null)
                .Update();

            // Delete existing auto-extracted reminders
            var postedLetters = await client
                .From<PostedLetter>()
                .Where(x => x.WeekNumber == 40 && x.Year == 2025)
                .Get();

            foreach (var letter in postedLetters.Models)
            {
                await client
                    .From<Reminder>()
                    .Where(x => x.WeekLetterId == letter.Id && x.Source == "auto_extracted")
                    .Delete();
            }

            Console.WriteLine("Week 40 reminders reset successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}