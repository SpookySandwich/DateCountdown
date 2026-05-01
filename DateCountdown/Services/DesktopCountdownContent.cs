namespace DateCountdown.Services;

internal sealed record DesktopCountdownContent(
    string CountdownId,
    string DaysLeft,
    string Title,
    double DaysTextSize,
    double TitleTextSize,
    double PositionX,
    double PositionY,
    double Scale,
    string Css);
