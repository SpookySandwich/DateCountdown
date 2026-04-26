using System;
using System.Globalization;
using System.Text;

namespace DateCountdown.Core
{
    public sealed class CountdownItem
    {
        public const string DefaultId = "default";
        public const int MaxTitleLength = 80;
        private const int MaxIdLength = 128;

        public CountdownItem(string? id, string? title, DateTimeOffset targetDate, bool toastEnabled = false)
        {
            Id = NormalizeId(id);
            Title = NormalizeTitle(title);
            TargetDate = targetDate;
            ToastEnabled = toastEnabled;
        }

        public string Id { get; }

        public string Title { get; }

        public DateTimeOffset TargetDate { get; }

        public bool ToastEnabled { get; }

        public CountdownItem With(string? title = null, DateTimeOffset? targetDate = null, bool? toastEnabled = null)
        {
            return new CountdownItem(
                Id,
                title ?? Title,
                targetDate ?? TargetDate,
                toastEnabled ?? ToastEnabled);
        }

        public static string NormalizeTitle(string? title)
        {
            string value = title?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            int textElementCount = 0;

            while (textElementCount < MaxTitleLength && enumerator.MoveNext())
            {
                builder.Append(enumerator.GetTextElement());
                textElementCount++;
            }

            return builder.ToString();
        }

        private static string NormalizeId(string? id)
        {
            string value = id?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return DefaultId;
            }

            return value.Length <= MaxIdLength ? value : value.Substring(0, MaxIdLength);
        }
    }
}
