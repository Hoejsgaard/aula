using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aula.Configuration;
using Aula.Integration;
using System.Net.Http.Headers;

public class TestTestChild1Direct
{
    public static async Task Main(string[] args)
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        var logger = loggerFactory.CreateLogger<TestTestChild1Direct>();

        // Create TestChild1's child object
        var testchild1 = new Child
        {
            FirstName = TestChild1,
            LastName = "Højsgaard",
            UniLogin = new UniLogin
            {
                Username = "testchild1439j",
                AuthType = AuthenticationType.Pictogram,
                PictogramSequence = new[] { "image1", "image2", "image3", "image4" }
            }
        };

        logger.LogInformation("🔍 Testing DIRECT pictogram authentication for {Name}", testchild1.FirstName);
        logger.LogInformation("Username: {Username}", testchild1.UniLogin.Username);
        logger.LogInformation("Pictogram sequence: {Sequence}", string.Join(" → ", testchild1.UniLogin.PictogramSequence!));

        // Create the pictogram client
        var client = new PictogramAuthenticatedClient(
            testchild1,
            testchild1.UniLogin.Username,
            testchild1.UniLogin.PictogramSequence!,
            loggerFactory.CreateLogger<PictogramAuthenticatedClient>()
        );

        try
        {
            // Attempt authentication
            logger.LogInformation("🚀 Starting authentication...");
            var loginSuccess = await client.LoginAsync();

            if (loginSuccess)
            {
                logger.LogInformation("✅ Authentication successful!");

                // Skip header check since HttpClient is protected

                // Try to fetch week letter
                logger.LogInformation("📚 Fetching week letter for current week...");
                var today = DateOnly.FromDateTime(DateTime.Now);
                logger.LogInformation("Date: {Date}, Week: {Week}", today, GetIsoWeekNumber(today));

                var weekLetter = await client.GetWeekLetter(today);

                logger.LogInformation("Week letter response type: {Type}", weekLetter.GetType().Name);

                if (weekLetter != null && weekLetter.HasValues)
                {
                    logger.LogInformation("✅ Week letter retrieved!");

                    // Check for ugebreve
                    if (weekLetter["ugebreve"] != null)
                    {
                        var ugebreve = weekLetter["ugebreve"];
                        logger.LogInformation("Found ugebreve array with {Count} items", ugebreve.Count());

                        if (ugebreve.Any())
                        {
                            var first = ugebreve.First();
                            logger.LogInformation("First item keys: {Keys}", string.Join(", ",
                                ((Newtonsoft.Json.Linq.JObject)first).Properties().Select(p => p.Name)));

                            var content = first["indhold"]?.ToString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                logger.LogInformation("Content length: {Length} characters", content.Length);
                                logger.LogInformation("Content preview: {Content}",
                                    content.Substring(0, Math.Min(200, content.Length)));
                            }
                        }
                    }
                    else if (weekLetter["UgePlan"] != null)
                    {
                        logger.LogInformation("Found UgePlan in response");
                    }
                    else if (weekLetter["errorMessage"] != null)
                    {
                        logger.LogWarning("Error in response: {Error}", weekLetter["errorMessage"]);
                    }
                    else
                    {
                        logger.LogInformation("Response keys: {Keys}", string.Join(", ",
                            weekLetter.Properties().Select(p => p.Name)));
                        logger.LogInformation("Full response: {Content}", weekLetter.ToString());
                    }
                }
                else
                {
                    logger.LogWarning("❌ No week letter content found or empty response");
                }

                // Try different weeks
                for (int weeksBack = 1; weeksBack <= 2; weeksBack++)
                {
                    var pastDate = today.AddDays(-7 * weeksBack);
                    logger.LogInformation("📚 Trying week {Week}/{Year}...",
                        GetIsoWeekNumber(pastDate), pastDate.Year);

                    var pastLetter = await client.GetWeekLetter(pastDate);
                    if (pastLetter != null && pastLetter.HasValues && pastLetter["ugebreve"] != null)
                    {
                        var ugebreve = pastLetter["ugebreve"];
                        logger.LogInformation("Week {Week}: Found {Count} letters",
                            GetIsoWeekNumber(pastDate), ugebreve.Count());
                    }
                }
            }
            else
            {
                logger.LogError("❌ Authentication failed!");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Exception during test");
        }
    }

    private static int GetIsoWeekNumber(DateOnly date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        return cal.GetWeekOfYear(dateTime, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}