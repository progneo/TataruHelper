using System;

using NUnit.Framework;

using Translation.Http;

namespace Translation.Tests.Http
{
    /// <summary>
    /// When a service asks to be left alone, the next line has to honour that.
    /// Before this existed, a refusal changed nothing: every following line
    /// knocked again and was refused again, so the rate the service objected to
    /// was the rate it kept receiving - and each of those lines waited out a
    /// round trip before it could be handed to another engine.
    /// </summary>
    [TestFixture]
    public class RefusalCooldownTests
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 11, 14, 0, 0, DateTimeKind.Utc);

        [Test]
        public void BeforeAnythingHappens_NothingIsHeldBack()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));

            Assert.That(sut.IsActiveAt(Now, out _), Is.False);
        }

        [Test]
        public void AfterARefusal_TheWaitRunsForItsWholeLength()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));
            sut.Record(Now);

            Assert.That(sut.IsActiveAt(Now, out var until), Is.True);
            Assert.That(until, Is.EqualTo(Now.AddSeconds(60)));
            Assert.That(sut.IsActiveAt(Now.AddSeconds(59), out _), Is.True);
        }

        [Test]
        public void OnceTheWaitIsOver_RequestsResumeOnTheirOwn()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));
            sut.Record(Now);

            Assert.That(sut.IsActiveAt(Now.AddSeconds(60), out _), Is.False);
            Assert.That(sut.IsActiveAt(Now.AddMinutes(5), out _), Is.False);
        }

        [Test]
        public void ARefusalDuringTheWait_PushesTheEndBack()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));
            sut.Record(Now);
            sut.Record(Now.AddSeconds(30));

            Assert.That(sut.IsActiveAt(Now.AddSeconds(61), out var until), Is.True);
            Assert.That(until, Is.EqualTo(Now.AddSeconds(90)));
        }

        /// <summary>
        /// A service that answers again has stopped objecting, so nothing should
        /// keep sitting out a wait that is no longer real.
        /// </summary>
        [Test]
        public void AnAnswerEndsTheWaitEarly()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));
            sut.Record(Now);
            Assume.That(sut.IsActiveAt(Now, out _), Is.True);

            sut.Clear();

            Assert.That(sut.IsActiveAt(Now, out _), Is.False);
        }

        [Test]
        public void TheReportedEndIsInUtc()
        {
            var sut = new RefusalCooldown(TimeSpan.FromSeconds(60));
            sut.Record(Now);

            sut.IsActiveAt(Now, out var until);

            Assert.That(until.Kind, Is.EqualTo(DateTimeKind.Utc),
                "the caller turns this into local time to show it");
        }
    }
}
