using System;

namespace DateCountdown.Core
{
    public sealed class CountdownItem
    {
        public const string DefaultId = "default";

        public CountdownItem(string? id, string? title, DateTimeOffset targetDate, bool toastEnabled = false)
        {
            Id = string.IsNullOrWhiteSpace(id) ? DefaultId : id;
            Title = title ?? string.Empty;
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
    }
}
