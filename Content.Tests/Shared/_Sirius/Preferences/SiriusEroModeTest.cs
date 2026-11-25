using System;
using Content.Shared._Sirius.Preferences;
using Content.Shared.Preferences;
using NUnit.Framework;

namespace Content.Tests.Shared._Sirius.Preferences
{
    // #Sirius Add: пороги ЕРП считаются в минутах общего времени игры.
    [TestFixture]
    public sealed class SiriusEroModeTest
    {
        [Test]
        public void UnlockThresholdsTest()
        {
            Assert.That(SiriusEroMode.None.IsUnlocked(TimeSpan.Zero), Is.True);

            Assert.That(SiriusEroMode.Light.IsUnlocked(TimeSpan.FromMinutes(119)), Is.False);
            Assert.That(SiriusEroMode.Light.IsUnlocked(TimeSpan.FromMinutes(120)), Is.True);

            Assert.That(SiriusEroMode.Hard.IsUnlocked(TimeSpan.FromMinutes(1199)), Is.False);
            Assert.That(SiriusEroMode.Hard.IsUnlocked(TimeSpan.FromMinutes(1200)), Is.True);
        }

        [Test]
        public void ProfileKeepsEroModeTest()
        {
            var profile = new HumanoidCharacterProfile();
            Assert.That(profile.Ero, Is.EqualTo(SiriusEroMode.None));

            var hard = profile.WithSiriusEro(SiriusEroMode.Hard);
            Assert.That(hard.Ero, Is.EqualTo(SiriusEroMode.Hard));
            Assert.That(hard.Clone().Ero, Is.EqualTo(SiriusEroMode.Hard));
            Assert.That(hard.MemberwiseEquals(profile), Is.False);
        }
    }
}
