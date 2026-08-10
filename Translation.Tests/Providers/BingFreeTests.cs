using System;

using NUnit.Framework;

using Translation.Providers.Bing;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// Bing has no key: the credentials are read off the translator page and
    /// expire. That makes two things worth pinning - that we read the page the
    /// way it is actually written, and that we give up quietly when it is not
    /// the page we expected, because a consent wall is a normal thing to get
    /// back and there is nothing the player could do about it.
    /// </summary>
    [TestFixture]
    public class BingFreeTests
    {
        private static readonly DateTime Now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

        private const string PageShape = """
            <html><head><script type="text/javascript">
            var _G = {IG:"A1B2C3D4E5F6", EventID:"x"};
            var params_AbusePreventionHelper = [1754827200000,"Tk9uY2VUb2tlbg==",3600000];
            </script></head>
            <body><div id="tta_outGDCont" data-iid="translator.5028"></div></body></html>
            """;

        [Test]
        public void PageValues_AreRead()
        {
            var sut = BingSessionCredentials.TryParse(PageShape, Now);

            Assert.That(sut, Is.Not.Null);
            Assert.That(sut.Ig, Is.EqualTo("A1B2C3D4E5F6"));
            Assert.That(sut.Iid, Is.EqualTo("translator.5028"));
            Assert.That(sut.Key, Is.EqualTo("1754827200000"));
            Assert.That(sut.Token, Is.EqualTo("Tk9uY2VUb2tlbg=="));
        }

        [Test]
        public void LifetimeIsTakenFromThePage()
        {
            var sut = BingSessionCredentials.TryParse(PageShape, Now);

            Assert.That(sut.ExpiresAtUtc, Is.EqualTo(Now.AddHours(1)));
        }

        /// <summary>
        /// Spent a minute early: a request that leaves just before the edge
        /// arrives just after it.
        /// </summary>
        [Test]
        public void CredentialsAreRetiredBeforeTheyActuallyExpire()
        {
            var sut = BingSessionCredentials.TryParse(PageShape, Now);

            Assert.That(sut.IsUsableAt(Now), Is.True);
            Assert.That(sut.IsUsableAt(Now.AddMinutes(58)), Is.True);
            Assert.That(sut.IsUsableAt(Now.AddSeconds(59 * 60 + 1)), Is.False);
            Assert.That(sut.IsUsableAt(Now.AddHours(2)), Is.False);
        }

        [Test]
        public void AnythingThatIsNotTheTranslatorPage_YieldsNothing()
        {
            Assert.That(BingSessionCredentials.TryParse(null, Now), Is.Null);
            Assert.That(BingSessionCredentials.TryParse("", Now), Is.Null);
            Assert.That(BingSessionCredentials.TryParse("<html>consent wall</html>", Now), Is.Null);

            // Everything but the abuse-prevention array: a partial read must not
            // produce credentials that fail confusingly later.
            Assert.That(
                BingSessionCredentials.TryParse(
                    """<script>var _G = {IG:"A1"};</script><div data-iid="translator.5028"></div>""", Now),
                Is.Null);
        }

        [Test]
        public void Translation_IsRead()
        {
            const string body = """
                [{"detectedLanguage":{"language":"en","score":1.0},
                  "translations":[{"text":"Добро пожаловать.","to":"ru"}]}]
                """;

            Assert.That(BingFreeTranslator.ParseContent(body), Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void AnythingThatIsNotATranslation_IsEmpty()
        {
            Assert.That(BingFreeTranslator.ParseContent("""{"statusCode":400}"""), Is.Empty);
            Assert.That(BingFreeTranslator.ParseContent("[]"), Is.Empty);
            Assert.That(BingFreeTranslator.ParseContent("<html>consent wall</html>"), Is.Empty);
            Assert.That(BingFreeTranslator.ParseContent(""), Is.Empty);
        }
    }
}
