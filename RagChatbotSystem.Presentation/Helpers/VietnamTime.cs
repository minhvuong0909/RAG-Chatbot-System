using System;

namespace RagChatbotSystem.Presentation.Helpers
{
    public static class VietnamTime
    {
        private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

        public static DateTime FromUtc(DateTime value)
        {
            var utcValue = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZone);
        }

        public static string Format(DateTime value, string format)
        {
            return FromUtc(value).ToString(format);
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone(
                "UTC+07",
                TimeSpan.FromHours(7),
                "UTC+07",
                "UTC+07");
        }
    }
}
