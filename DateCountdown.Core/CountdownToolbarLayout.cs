using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DateCountdown.Core;

public sealed class CountdownToolbarLayout
{
    private const int DefaultPrimaryActionCount = 4;

    private static readonly IReadOnlyList<CountdownToolbarAction> DefaultActions =
    [
        CountdownToolbarAction.Add,
        CountdownToolbarAction.Edit,
        CountdownToolbarAction.Delete,
        CountdownToolbarAction.StartupNotification,
        CountdownToolbarAction.GlanceSurface,
        CountdownToolbarAction.DesktopOverlay
    ];

    public static readonly CountdownToolbarLayout Default = new(
        DefaultActions.Select(action => (CountdownToolbarAction?)action),
        DefaultPrimaryActionCount);

    public CountdownToolbarLayout(
        IEnumerable<CountdownToolbarAction?>? actions = null,
        int? primaryActionCount = null)
    {
        List<CountdownToolbarAction> normalizedActions = NormalizeActions(actions).ToList();
        Actions = new ReadOnlyCollection<CountdownToolbarAction>(normalizedActions);
        PrimaryActionCount = Math.Clamp(
            primaryActionCount ?? DefaultPrimaryActionCount,
            0,
            normalizedActions.Count);
    }

    public IReadOnlyList<CountdownToolbarAction> Actions { get; }

    public int PrimaryActionCount { get; }

    public IReadOnlyList<CountdownToolbarAction> PrimaryActions => Actions.Take(PrimaryActionCount).ToList();

    public IReadOnlyList<CountdownToolbarAction> OverflowActions => Actions.Skip(PrimaryActionCount).ToList();

    public bool IsPrimary(CountdownToolbarAction action)
    {
        return Actions.Take(PrimaryActionCount).Contains(action);
    }

    public CountdownToolbarLayout With(
        IEnumerable<CountdownToolbarAction?>? actions = null,
        int? primaryActionCount = null)
    {
        return new CountdownToolbarLayout(
            actions ?? Actions.Select(action => (CountdownToolbarAction?)action),
            primaryActionCount ?? PrimaryActionCount);
    }

    public static string SerializeActions(IEnumerable<CountdownToolbarAction> actions)
    {
        return string.Join(",", NormalizeActions(actions.Select(action => (CountdownToolbarAction?)action)));
    }

    public static IReadOnlyList<CountdownToolbarAction> ParseActions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default.Actions;
        }

        IEnumerable<CountdownToolbarAction?> actions = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseAction);
        return NormalizeActions(actions).ToList();
    }

    private static CountdownToolbarAction? ParseAction(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out CountdownToolbarAction action)
            ? action
            : null;
    }

    private static IEnumerable<CountdownToolbarAction> NormalizeActions(IEnumerable<CountdownToolbarAction?>? actions)
    {
        HashSet<CountdownToolbarAction> usedActions = [];
        foreach (CountdownToolbarAction? action in actions ?? DefaultActions.Select(action => (CountdownToolbarAction?)action))
        {
            if (action.HasValue &&
                Enum.IsDefined(action.Value) &&
                usedActions.Add(action.Value))
            {
                yield return action.Value;
            }
        }

        foreach (CountdownToolbarAction action in DefaultActions)
        {
            if (usedActions.Add(action))
            {
                yield return action;
            }
        }
    }
}
