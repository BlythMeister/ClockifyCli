using ClockifyCli.Utilities;

// Debug test to understand the logic
Console.WriteLine($"Is '08:00' ambiguous? {IntelligentTimeParser.IsAmbiguousTime("08:00")}");
Console.WriteLine($"Is '10:00' ambiguous? {IntelligentTimeParser.IsAmbiguousTime("10:00")}");
Console.WriteLine($"Is '9:30' ambiguous? {IntelligentTimeParser.IsAmbiguousTime("9:30")}");
Console.WriteLine($"Is '2:15' ambiguous? {IntelligentTimeParser.IsAmbiguousTime("2:15")}");

var contextTime = new DateTime(2024, 1, 15, 14, 0, 0); // 2:00 PM
var success = IntelligentTimeParser.TryParseTime("3:30", out var result, contextTime, isStartTime: true);

Console.WriteLine($"Input: 3:30, Context: 2:00 PM");
Console.WriteLine($"Success: {success}");
Console.WriteLine($"Result: {result}");
Console.WriteLine($"Expected: 15:30:00 (3:30 PM)");

// Test another case
var startTime = new DateTime(2024, 1, 15, 9, 0, 0);
success = IntelligentTimeParser.TryParseTime("5:30", out result, startTime, isStartTime: false);

Console.WriteLine($"\nInput: 5:30, Start: 9:00 AM (end time)");
Console.WriteLine($"Success: {success}");
Console.WriteLine($"Result: {result}");
Console.WriteLine($"Expected: 17:30:00 (5:30 PM)");
