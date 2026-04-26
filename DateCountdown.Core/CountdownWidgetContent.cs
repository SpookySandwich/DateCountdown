using System;
using System.Globalization;
using System.Text;

namespace DateCountdown.Core
{
    public static class CountdownWidgetContent
    {
        public const string DefinitionId = "DateCountdown_Widget";

        public static string BuildTemplateJson()
        {
            return @"
{
  ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
  ""type"": ""AdaptiveCard"",
  ""version"": ""1.5"",
  ""body"": [
    {
      ""type"": ""TextBlock"",
      ""text"": ""${daysText}"",
      ""size"": ""ExtraLarge"",
      ""weight"": ""Bolder"",
      ""wrap"": true,
      ""maxLines"": 2
    },
    {
      ""type"": ""TextBlock"",
      ""text"": ""${title}"",
      ""isSubtle"": true,
      ""wrap"": true,
      ""maxLines"": 2,
      ""spacing"": ""Small""
    },
    {
      ""type"": ""TextBlock"",
      ""text"": ""${targetDateText}"",
      ""size"": ""Small"",
      ""isSubtle"": true,
      ""wrap"": true,
      ""spacing"": ""Medium""
    }
  ]
}";
        }

        public static string BuildDataJson(string title, DateTimeOffset targetDate, DateTimeOffset now, CountdownDisplayText displayText)
        {
            int daysLeft = CountdownLogic.CalculateDaysLeft(targetDate, now);
            string displayTitle = string.IsNullOrWhiteSpace(title) ? displayText.DefaultTitle : title;
            string targetDateText = targetDate.Date.ToString("d", CultureInfo.CurrentCulture);

            return "{" +
                "\"daysText\":\"" + JsonEscape(displayText.FormatDaysLeft(daysLeft)) + "\"," +
                "\"title\":\"" + JsonEscape(displayTitle) + "\"," +
                "\"targetDateText\":\"" + JsonEscape(targetDateText) + "\"" +
                "}";
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\b':
                        builder.Append(@"\b");
                        break;
                    case '\f':
                        builder.Append(@"\f");
                        break;
                    case '\n':
                        builder.Append(@"\n");
                        break;
                    case '\r':
                        builder.Append(@"\r");
                        break;
                    case '\t':
                        builder.Append(@"\t");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
