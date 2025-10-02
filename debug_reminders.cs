using System;
using System.Globalization;

// Quick test to understand the date parsing issue
public class Program
{
    public static void Main()
    {
        var now = DateTime.Now;
        Console.WriteLine($"Current date: {now:yyyy-MM-dd} ({now:dddd})");
        Console.WriteLine($"Week number: {ISOWeek.GetWeekOfYear(now)}");

        // Test Danish day names
        var culture = new CultureInfo("da-DK");
        for (int i = 0; i < 7; i++)
        {
            var date = now.Date.AddDays(i - (int)now.DayOfWeek + 1); // Start from Monday
            var dayName = date.ToString("dddd", culture);
            Console.WriteLine($"{date:yyyy-MM-dd} = {dayName}");
        }

        // Test dates that should be Thursday
        var thursday = DateTime.Parse("2025-10-03"); // Tomorrow
        Console.WriteLine($"\n2025-10-03 is: {thursday.ToString("dddd", culture)}");

        // Test dates that might be parsed as Sunday
        var sunday = DateTime.Parse("2025-10-06");
        Console.WriteLine($"2025-10-06 is: {sunday.ToString("dddd", culture)}");
    }
}