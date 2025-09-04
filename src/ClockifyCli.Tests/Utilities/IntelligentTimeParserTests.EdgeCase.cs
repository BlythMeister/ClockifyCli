using NUnit.Framework;
using ClockifyCli.Utilities;

namespace ClockifyCli.Tests.Utilities
{
    [TestFixture]
    public class IntelligentTimeParserEdgeCaseTests
    {
        [Test]
        public void TryParseEndTime_WithMorningStartAndMorningEnd_ShouldPreferAM()
        {
            // Arrange
            var startTime = new DateTime(2024, 1, 1, 8, 0, 0); // 8:00 AM
            var input = "10:00";

            // Act
            var success = IntelligentTimeParser.TryParseEndTime(input, out var result, startTime);

            // Assert
            Assert.That(success, Is.True, "Should successfully parse '10:00' as end time");
            Assert.That(result, Is.EqualTo(TimeSpan.FromHours(10)), "Should interpret as 10:00 AM, not 10:00 PM");
        }

        [Test]
        public void TryParseEndTime_WithMorningStartAndLaterEnd_ShouldPreferPM()
        {
            // Arrange
            var startTime = new DateTime(2024, 1, 1, 8, 0, 0); // 8:00 AM
            var input = "6:00"; // Would be unreasonable as 6:00 AM after 8:00 AM start

            // Act
            var success = IntelligentTimeParser.TryParseEndTime(input, out var result, startTime);

            // Assert
            Assert.That(success, Is.True, "Should successfully parse '6:00' as end time");
            Assert.That(result, Is.EqualTo(TimeSpan.FromHours(18)), "Should interpret as 6:00 PM for longer work session");
        }

        [Test]
        public void TryParseStartTime_At3PM_Should_Interpret1045_As_AM()
        {
            // Arrange
            var currentTime = new DateTime(2024, 1, 1, 15, 0, 0); // 3:00 PM
            var input = "10:45";

            // Act
            var success = IntelligentTimeParser.TryParseStartTime(input, out var result, currentTime);

            // Assert
            Assert.That(success, Is.True, "Should successfully parse '10:45' as start time");
            Assert.That(result, Is.EqualTo(new TimeSpan(10, 45, 0)), "Should interpret as 10:45 AM, not 10:45 PM");
        }

        [Test]
        public void TryParseStartTime_At3PM_Should_Interpret900_As_AM()
        {
            // Arrange
            var currentTime = new DateTime(2024, 1, 1, 15, 0, 0); // 3:00 PM
            var input = "9:00";

            // Act
            var success = IntelligentTimeParser.TryParseStartTime(input, out var result, currentTime);

            // Assert
            Assert.That(success, Is.True, "Should successfully parse '9:00' as start time");
            Assert.That(result, Is.EqualTo(new TimeSpan(9, 0, 0)), "Should interpret as 9:00 AM, not 9:00 PM");
        }

        [Test]
        public void TryParseStartTime_At3PM_Should_Interpret200_As_PM()
        {
            // Arrange  
            var currentTime = new DateTime(2024, 1, 1, 15, 0, 0); // 3:00 PM
            var input = "2:00";

            // Act
            var success = IntelligentTimeParser.TryParseStartTime(input, out var result, currentTime);

            // Assert
            Assert.That(success, Is.True, "Should successfully parse '2:00' as start time");
            Assert.That(result, Is.EqualTo(new TimeSpan(14, 0, 0)), "Should interpret as 2:00 PM (close to current time)");
        }

        [Test]
        public void TryParseTime_WithSingleLetterAM_ShouldParseCorrectly()
        {
            // Act & Assert
            Assert.That(IntelligentTimeParser.TryParseTime("1:15a", out var result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(1, 15, 0)), "Should parse '1:15a' as 1:15 AM");
            
            Assert.That(IntelligentTimeParser.TryParseTime("12:30a", out result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(0, 30, 0)), "Should parse '12:30a' as 12:30 AM (midnight)");
        }

        [Test]
        public void TryParseTime_WithSingleLetterPM_ShouldParseCorrectly()
        {
            // Act & Assert
            Assert.That(IntelligentTimeParser.TryParseTime("1:15p", out var result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(13, 15, 0)), "Should parse '1:15p' as 1:15 PM (13:15)");
            
            Assert.That(IntelligentTimeParser.TryParseTime("12:30p", out result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(12, 30, 0)), "Should parse '12:30p' as 12:30 PM");
        }

        [Test]
        public void TryParseTime_WithUppercaseSingleLetter_ShouldParseCorrectly()
        {
            // Act & Assert
            Assert.That(IntelligentTimeParser.TryParseTime("2:45A", out var result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(2, 45, 0)), "Should parse '2:45A' as 2:45 AM");
            
            Assert.That(IntelligentTimeParser.TryParseTime("2:45P", out result), Is.True);
            Assert.That(result, Is.EqualTo(new TimeSpan(14, 45, 0)), "Should parse '2:45P' as 2:45 PM (14:45)");
        }
    }
}
