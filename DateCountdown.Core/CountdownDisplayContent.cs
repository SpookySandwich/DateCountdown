namespace DateCountdown.Core
{
    public readonly record struct CountdownDisplayContent(
        string PrimaryText,
        string SecondaryText,
        string AccessibleText,
        CountdownDisplayLayoutKind LayoutKind = CountdownDisplayLayoutKind.SingleLine)
    {
        public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);
    }

    public enum CountdownDisplayLayoutKind
    {
        SingleLine,
        DaysAndHours
    }
}
