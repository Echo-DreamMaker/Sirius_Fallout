using Content.Server._Sirius.Dirtiness;
using Content.Shared._Sirius.Dirtiness;
using NUnit.Framework;

namespace Content.Tests._Sirius;

// #Sirius Add: guards the seven cleanliness thresholds used by the HUD and SPECIAL modifier.
public sealed class SiriusDirtyStateTests
{
    [TestCase(0f, SiriusDirtyState.Animal)]
    [TestCase(5f, SiriusDirtyState.Animal)]
    [TestCase(5.01f, SiriusDirtyState.VeryDirty)]
    [TestCase(10f, SiriusDirtyState.VeryDirty)]
    [TestCase(10.01f, SiriusDirtyState.Dirty)]
    [TestCase(15f, SiriusDirtyState.Dirty)]
    [TestCase(15.01f, SiriusDirtyState.SlightlySmelly)]
    [TestCase(20f, SiriusDirtyState.SlightlySmelly)]
    [TestCase(20.01f, SiriusDirtyState.RelativelyClean)]
    [TestCase(25f, SiriusDirtyState.RelativelyClean)]
    [TestCase(25.01f, SiriusDirtyState.Clean)]
    [TestCase(30f, SiriusDirtyState.Clean)]
    [TestCase(30.01f, SiriusDirtyState.Fragrant)]
    [TestCase(40f, SiriusDirtyState.Fragrant)]
    public void CleanlinessMapsToExpectedState(float cleanliness, SiriusDirtyState expected)
    {
        Assert.That(SiriusDirtySystem.GetState(cleanliness), Is.EqualTo(expected));
    }

    [TestCase("Ghoul", 1f, 2f, 2f)]
    [TestCase("GhoulGlowing", 1f, 2f, 2f)]
    [TestCase("Human", 1f, 2f, 1f)]
    [TestCase("SuperMutant", 1f, 2f, 1f)]
    [TestCase(null, 1f, 2f, 1f)]
    [TestCase("Ghoul", 2f, 2f, 4f)]
    public void SpeciesDecayMultiplierAppliesOnlyToGhouls(string species, float multiplier, float ghoulMultiplier,
        float expected)
    {
        Assert.That(SiriusDirtySystem.ApplySpeciesDecayMultiplier(species, multiplier, ghoulMultiplier),
            Is.EqualTo(expected).Within(0.0001f));
    }
}
