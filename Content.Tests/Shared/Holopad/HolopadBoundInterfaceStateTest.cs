using System.Collections.Generic;
using Content.Shared.Holopad;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Tests.Shared.Holopad;

// #Misfits Add - HolopadSystem pushes a fresh HolopadBoundInterfaceState every second;
// value equality is what lets SetUiState skip the push when nothing changed. If these
// break, every holopad terminal re-dirties its UserInterfaceComponent each second and
// client-side UIs on the same entity get constantly re-rendered.
[TestFixture]
[TestOf(typeof(HolopadBoundInterfaceState))]
public sealed class HolopadBoundInterfaceStateTest
{
    private static NetEntity Ent(int id) => new(id);

    [Test]
    public void EqualContactListsAreEqual()
    {
        var a = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string> { [Ent(1)] = "one", [Ent(2)] = "two" });
        var b = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string> { [Ent(2)] = "two", [Ent(1)] = "one" });

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void DifferentContactListsAreNotEqual()
    {
        var a = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string> { [Ent(1)] = "one" });
        var renamed = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string> { [Ent(1)] = "renamed" });
        var extra = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string> { [Ent(1)] = "one", [Ent(2)] = "two" });
        var empty = new HolopadBoundInterfaceState(new Dictionary<NetEntity, string>());

        Assert.That(a, Is.Not.EqualTo(renamed));
        Assert.That(a, Is.Not.EqualTo(extra));
        Assert.That(extra, Is.Not.EqualTo(a));
        Assert.That(a, Is.Not.EqualTo(empty));
        Assert.That(a, Is.Not.EqualTo(null));
    }
}
