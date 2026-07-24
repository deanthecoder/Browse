// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Browse.Services;
using DTC.Core;

namespace Browse.Tests;

/// <summary>
/// Verifies launch forwarding between Browse processes.
/// </summary>
/// <remarks>
/// Unique pipe names keep tests isolated from a running development instance.
/// </remarks>
[TestFixture]
public sealed class SingleInstanceLaunchServerTests
{
    [Test]
    public void CheckSecondInstanceIsRejected()
    {
        var applicationName = $"Browse.Tests.{Guid.NewGuid():N}";
        using var first = SingleInstanceGuard.TryAcquire(applicationName);
        using var second = SingleInstanceGuard.TryAcquire(applicationName);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
        });
    }

    [Test]
    public async Task CheckLaunchArgumentsAreForwarded()
    {
        var received = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeName = $"BTest.{Guid.NewGuid():N}"[..14];
        using var server = new SingleInstanceLaunchServer(arguments => received.TrySetResult(arguments), pipeName);

        var sent = SingleInstanceLaunchServer.TrySend(["--test", "/tmp/My Folder"], pipeName);
        var arguments = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.True);
            Assert.That(arguments, Is.EqualTo(new[] { "--test", "/tmp/My Folder" }));
        });
    }
}
