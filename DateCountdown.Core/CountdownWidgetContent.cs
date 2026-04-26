using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DateCountdown.Core
{
    public static class CountdownWidgetContent
    {
        public const string DefinitionId = "DateCountdown_Widget";
        private const int MaxMediumListItems = 4;
        private const int MaxLargeListItems = 7;

        public static string BuildTemplateJson()
        {
            return BuildTemplateJson(0, CountdownWidgetSize.Small);
        }

        public static string BuildTemplateJson(CountdownState state)
        {
            return BuildTemplateJson(state, CountdownWidgetSize.Large);
        }

        public static string BuildTemplateJson(CountdownState state, CountdownWidgetSize size)
        {
            return BuildTemplateJson(GetListItems(state, size).Count, size);
        }

        private static string BuildTemplateJson(int listItemCount, CountdownWidgetSize size)
        {
            string contentWidth = size == CountdownWidgetSize.Large ? "252px" : "224px";
            string contentMinHeight = listItemCount > 0
                ? size == CountdownWidgetSize.Large ? "244px" : "174px"
                : size == CountdownWidgetSize.Small ? "112px" : "132px";
            string bodyMinHeight = size switch
            {
                CountdownWidgetSize.Small => "126px",
                CountdownWidgetSize.Medium => "160px",
                _ => "280px"
            };

            return @"
{
  ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
  ""type"": ""AdaptiveCard"",
  ""version"": ""1.5"",
  ""verticalContentAlignment"": ""Center"",
  ""body"": [
    {
      ""type"": ""Container"",
      ""spacing"": ""None"",
      ""minHeight"": """ + bodyMinHeight + @""",
      ""verticalContentAlignment"": ""Center"",
      ""items"": [
        {
          ""type"": ""ColumnSet"",
          ""spacing"": ""None"",
          ""columns"": [
            {
              ""type"": ""Column"",
              ""width"": ""stretch"",
              ""items"": []
            },
            {
              ""type"": ""Column"",
              ""width"": """ + contentWidth + @""",
              ""items"": [
                {
                  ""type"": ""Container"",
                  ""minHeight"": """ + contentMinHeight + @""",
                  ""verticalContentAlignment"": ""Center"",
                  ""items"": [
                    {
                      ""type"": ""TextBlock"",
                      ""text"": ""${daysText}"",
                      ""size"": ""Large"",
                      ""horizontalAlignment"": ""Center"",
                      ""wrap"": true,
                      ""maxLines"": 1
                    },
                    {
                      ""type"": ""TextBlock"",
                      ""text"": ""${title}"",
                      ""isSubtle"": true,
                      ""horizontalAlignment"": ""Center"",
                      ""wrap"": true,
                      ""maxLines"": 2,
                      ""spacing"": """ + (size == CountdownWidgetSize.Small ? "None" : "Small") + @"""
                    },
                    {
                      ""type"": ""TextBlock"",
                      ""text"": ""${targetDateText}"",
                      ""size"": ""Small"",
                      ""isSubtle"": true,
                      ""horizontalAlignment"": ""Center"",
                      ""wrap"": true,
                      ""spacing"": ""Small""
                    }
" + BuildListContainerJson(listItemCount) + @"
                  ]
                }
              ]
            },
            {
              ""type"": ""Column"",
              ""width"": ""stretch"",
              ""items"": []
            }
          ]
        }
      ]
    }
  ]
}";
        }

        public static string BuildDataJson(string title, DateTimeOffset targetDate, DateTimeOffset now, CountdownDisplayText displayText)
        {
            return BuildDataJson(
                new CountdownState(title, targetDate, tileEnabled: false, toastEnabled: false),
                now,
                displayText);
        }

        public static string BuildDataJson(CountdownState state, DateTimeOffset now, CountdownDisplayText displayText)
        {
            return BuildDataJson(state, now, displayText, CountdownWidgetSize.Large);
        }

        public static string BuildDataJson(CountdownState state, DateTimeOffset now, CountdownDisplayText displayText, CountdownWidgetSize size)
        {
            CountdownItem selectedCountdown = state.SelectedCountdown;
            int daysLeft = CountdownLogic.CalculateDaysLeft(selectedCountdown.TargetDate, now);
            string displayTitle = GetDisplayTitle(selectedCountdown.Title, displayText);
            string targetDateText = selectedCountdown.TargetDate.Date.ToString("d", CultureInfo.CurrentCulture);
            IReadOnlyList<CountdownItem> listItems = GetListItems(state, size);

            StringBuilder builder = new StringBuilder(160 + listItems.Count * 80);
            builder.Append("{" +
                "\"daysText\":\"" + JsonEscape(displayText.FormatDaysLeft(daysLeft)) + "\"," +
                "\"title\":\"" + JsonEscape(displayTitle) + "\"," +
                "\"targetDateText\":\"" + JsonEscape(targetDateText) + "\"");

            for (int i = 0; i < listItems.Count; i++)
            {
                CountdownItem item = listItems[i];
                int itemDaysLeft = CountdownLogic.CalculateDaysLeft(item.TargetDate, now);
                builder.Append(",\"item");
                builder.Append(i.ToString(CultureInfo.InvariantCulture));
                builder.Append("Title\":\"");
                builder.Append(JsonEscape(GetDisplayTitle(item.Title, displayText)));
                builder.Append("\",\"item");
                builder.Append(i.ToString(CultureInfo.InvariantCulture));
                builder.Append("DaysText\":\"");
                builder.Append(JsonEscape(displayText.FormatDaysLeft(itemDaysLeft)));
                builder.Append("\"");
            }

            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildListContainerJson(int listItemCount)
        {
            if (listItemCount <= 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(@",
    {
      ""type"": ""Container"",
      ""spacing"": ""Medium"",
      ""separator"": true,
      ""items"": [");

            for (int i = 0; i < listItemCount; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                string index = i.ToString(CultureInfo.InvariantCulture);
                builder.Append(@"
        {
          ""type"": ""ColumnSet"",
          ""spacing"": ""Small"",
          ""columns"": [
            {
              ""type"": ""Column"",
              ""width"": ""stretch"",
              ""items"": [
                {
                  ""type"": ""TextBlock"",
                  ""text"": ""${item" + index + @"Title}"",
                  ""size"": ""Small"",
                  ""wrap"": false,
                  ""maxLines"": 1
                }
              ]
            },
            {
              ""type"": ""Column"",
              ""width"": ""auto"",
              ""items"": [
                {
                  ""type"": ""TextBlock"",
                  ""text"": ""${item" + index + @"DaysText}"",
                  ""size"": ""Small"",
                  ""isSubtle"": true,
                  ""horizontalAlignment"": ""Right"",
                  ""wrap"": false,
                  ""maxLines"": 1
                }
              ]
            }
          ]
        }");
            }

            builder.Append(@"
      ]
    }");

            return builder.ToString();
        }

        private static IReadOnlyList<CountdownItem> GetListItems(CountdownState state, CountdownWidgetSize size)
        {
            int maxListItems = size switch
            {
                CountdownWidgetSize.Small => 0,
                CountdownWidgetSize.Medium => MaxMediumListItems,
                _ => MaxLargeListItems
            };

            return state.Countdowns
                .Where(item => !string.Equals(item.Id, state.SelectedCountdownId, StringComparison.Ordinal))
                .OrderBy(item => item.TargetDate.Date)
                .Take(maxListItems)
                .ToList();
        }

        private static string GetDisplayTitle(string title, CountdownDisplayText displayText)
        {
            return string.IsNullOrWhiteSpace(title) ? displayText.DefaultTitle : title;
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
