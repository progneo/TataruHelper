using NUnit.Framework;

using Translation.Providers.DeepL;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// A refusal reaches us in one of two shapes, and only one of them is from
    /// DeepL. The endpoint answers JSON naming what it objects to; something
    /// standing in front of it answers a web page. Telling them apart is the
    /// whole question when translation stops working, so the log has to carry
    /// the part a person would have read - not the stylesheet that happens to
    /// come first.
    /// </summary>
    [TestFixture]
    public class DeepLRefusalDescriptionTests
    {
        [Test]
        public void TheEndpointsOwnRefusal_IsKeptAsItIs()
        {
            const string body = """{"jsonrpc":"2.0","error":{"code":1042911,"message":"Too many requests."}}""";

            Assert.That(DeepLTranslator.DescribeRefusal(body), Is.EqualTo(body));
        }

        /// <summary>
        /// The page that prompted this: several hundred characters of styling
        /// before a word of explanation, which the previous cut-off returned
        /// instead of the explanation.
        /// </summary>
        [Test]
        public void APageFromSomethingInTheWay_YieldsItsTitleAndItsWords()
        {
            const string body = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8"/>
                    <title>Page Load Error</title>
                    <style>
                        .button { background-color: #0f2b46; border: none; color: #fff; padding: 15px; }
                        .banner { text-align: center; }
                    </style>
                </head>
                <body><h1>Too many requests</h1><p>Your network is sending too many requests.</p></body>
                </html>
                """;

            var described = DeepLTranslator.DescribeRefusal(body);

            Assert.That(described, Does.StartWith("[html] Page Load Error ::"));
            Assert.That(described, Does.Contain("Too many requests"));
            Assert.That(described, Does.Contain("Your network is sending too many requests."));
            Assert.That(described, Does.Not.Contain("background-color"), "the stylesheet is not the message");
            Assert.That(described, Does.Not.Contain("<"), "no markup survives into the log");
        }

        [Test]
        public void AVeryLongPage_IsCutToSomethingTheLogCanHold()
        {
            var body = "<html><head><title>Blocked</title></head><body>" +
                       new string('x', 5000) + "</body></html>";

            var described = DeepLTranslator.DescribeRefusal(body);

            Assert.That(described.Length, Is.LessThanOrEqualTo(301));
            Assert.That(described, Does.StartWith("[html] Blocked ::"));
        }

        [Test]
        public void NothingAtAll_SaysSo()
        {
            Assert.That(DeepLTranslator.DescribeRefusal(""), Is.EqualTo("(empty body)"));
            Assert.That(DeepLTranslator.DescribeRefusal(null), Is.EqualTo("(empty body)"));
            Assert.That(DeepLTranslator.DescribeRefusal("   "), Is.EqualTo("(empty body)"));
        }
    }
}
