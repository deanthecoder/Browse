// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Browse.Models;
using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

/// <summary>
/// Verifies calendar-oriented ordering and headings for modified-date groups.
/// </summary>
/// <remarks>
/// A fixed Monday-first calendar makes boundary behavior deterministic on every test machine.
/// </remarks>
[TestFixture]
public sealed class DateModifiedGroupingTests
{
    [Test]
    public void CheckItemsAreGroupedByCalendarPeriodAndAlphabetizedTogether()
    {
        using var temp = new TempDirectory();
        var now = new DateTime(2026, 8, 19, 12, 0, 0);
        var items = new[]
        {
            CreateItem(temp, "today-folder", now.AddHours(-2), true),
            CreateItem(temp, "today.txt", now.AddHours(-1)),
            CreateItem(temp, "yesterday.txt", now.AddDays(-1)),
            CreateItem(temp, "week-b.txt", new DateTime(2026, 8, 17, 9, 0, 0)),
            CreateItem(temp, "week-a.txt", new DateTime(2026, 8, 17, 8, 0, 0)),
            CreateItem(temp, "last-week.txt", new DateTime(2026, 8, 10, 8, 0, 0)),
            CreateItem(temp, "month.txt", new DateTime(2026, 8, 2, 8, 0, 0)),
            CreateItem(temp, "earlier.txt", new DateTime(2026, 7, 31, 8, 0, 0))
        };

        var result = DateModifiedGrouping.Apply(items, true, now, DayOfWeek.Monday);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(item => item.Name), Is.EqualTo(new[]
            {
                "today-folder", "today.txt", "yesterday.txt", "week-a.txt", "week-b.txt",
                "last-week.txt", "month.txt", "earlier.txt"
            }));
            Assert.That(result.Select(item => item.GroupHeading), Is.EqualTo(new[]
            {
                "Today", null, "Yesterday", "This Week", null, "Last Week", "This Month", "Earlier"
            }));
        });
    }

    [Test]
    public void CheckDisabledGroupingRestoresGlobalAlphabeticalOrder()
    {
        using var temp = new TempDirectory();
        var now = new DateTime(2026, 8, 19, 12, 0, 0);
        var items = new[]
        {
            CreateItem(temp, "zulu.txt", now),
            CreateItem(temp, "alpha.txt", now.AddYears(-1))
        };

        var result = DateModifiedGrouping.Apply(items, false, now, DayOfWeek.Monday);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(item => item.Name), Is.EqualTo(new[] { "alpha.txt", "zulu.txt" }));
            Assert.That(result.Select(item => item.GroupHeading), Is.All.Null);
        });
    }

    private static BrowserItem CreateItem(
        DirectoryInfo directory,
        string name,
        DateTime modified,
        bool isDirectory = false)
    {
        FileSystemInfo info;
        var path = Path.Combine(directory.FullName, name);
        if (isDirectory)
        {
            info = Directory.CreateDirectory(path);
            Directory.SetLastWriteTime(path, modified);
        }
        else
        {
            File.WriteAllText(path, name);
            File.SetLastWriteTime(path, modified);
            info = new FileInfo(path);
        }
        info.Refresh();
        return new BrowserItem(info);
    }
}
