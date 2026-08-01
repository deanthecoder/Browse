// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Globalization;
using Browse.Models;

namespace Browse.Services;

/// <summary>
/// Orders browser items into calendar-oriented last-modified groups.
/// </summary>
/// <remarks>
/// Only the first real item in each group carries a heading, keeping headers outside selection and keyboard navigation.
/// </remarks>
internal static class DateModifiedGrouping
{
    public static IReadOnlyList<BrowserItem> Apply(
        IReadOnlyList<BrowserItem> items,
        bool enabled,
        DateTime now,
        DayOfWeek? firstDayOfWeek = null)
    {
        if (!enabled)
        {
            return items
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        var weekStart = firstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var grouped = items
            .Select(item => (Item: item, Group: GetGroup(item.LastWriteTime, now, weekStart)))
            .OrderBy(entry => entry.Group.Order)
            .ThenBy(entry => entry.Item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var result = new BrowserItem[grouped.Length];
        for (var index = 0; index < grouped.Length; index++)
        {
            var showHeading = index == 0 || grouped[index - 1].Group.Order != grouped[index].Group.Order;
            result[index] = showHeading
                ? grouped[index].Item.WithGroupHeading(grouped[index].Group.Label)
                : grouped[index].Item;
        }
        return result;
    }

    private static (int Order, string Label) GetGroup(DateTime modified, DateTime now, DayOfWeek firstDayOfWeek)
    {
        var today = now.Date;
        var date = modified.Date;
        var thisWeek = today.AddDays(-((7 + (int)today.DayOfWeek - (int)firstDayOfWeek) % 7));
        if (date >= today)
            return (0, "Today");
        if (date == today.AddDays(-1))
            return (1, "Yesterday");
        if (date >= thisWeek)
            return (2, "This Week");
        if (date >= thisWeek.AddDays(-7))
            return (3, "Last Week");
        if (date >= new DateTime(today.Year, today.Month, 1))
            return (4, "This Month");
        return (5, "Earlier");
    }
}
