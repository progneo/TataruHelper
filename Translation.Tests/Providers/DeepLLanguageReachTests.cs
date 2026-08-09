using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using NUnit.Framework;

using Translation.Models;
using Translation.Providers.DeepL;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// Asks DeepL's free endpoint whether it really translates the languages
    /// the list offers.
    ///
    /// Through the provider rather than through a request written here: the
    /// endpoint tells clients apart by the shape of what they send - it adjusts
    /// a timestamp by the number of letter i's and spaces the "method" key
    /// differently depending on the request id - and a copy of that logic in a
    /// test would be testing the copy. A hand-made request gets HTTP 429.
    ///
    /// Explicit because it goes out to the network and because asking about
    /// many languages at once is what rate limiting is for. Run it against a
    /// few at a time:
    ///
    ///   dotnet test --filter FullyQualifiedName~DeepLLanguageReach
    ///     -e DEEPL_PROBE=AR,HE,VI,TH,HI,SW
    /// </summary>
    [TestFixture, Explicit]
    public class DeepLLanguageReachTests
    {
        [Test]
        public async Task TheLanguagesOffered_AreTranslated()
        {
            var requested = Environment.GetEnvironmentVariable("DEEPL_PROBE");
            if (string.IsNullOrWhiteSpace(requested))
            {
                Assert.Ignore("Set DEEPL_PROBE to a comma-separated list of language codes.");
            }

            var translator = new DeepLTranslator(null, new Translation.Settings.TranslationSettings());
            var results = new List<string>();
            var failures = new List<string>();

            foreach (var code in requested.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                string answer;
                try
                {
                    answer = await translator.TranslateAsync(
                        "The wood is watching.", "EN", code, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    answer = null;
                    results.Add($"{code,-6} threw {ex.GetType().Name}: {ex.Message}");
                    failures.Add(code);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    results.Add($"{code,-6} no answer");
                    failures.Add(code);
                }
                else
                {
                    results.Add($"{code,-6} {answer}");
                }

                // The endpoint rate-limits, and a burst is what triggers it.
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }

            foreach (var line in results)
            {
                TestContext.Out.WriteLine(line);
            }

            Assert.That(failures, Is.Empty,
                "DeepL did not translate: " + string.Join(", ", failures));
        }

        /// <summary>Every code the application offers for DeepL, for reference.</summary>
        [Test]
        public void TheListIsReadable()
        {
            var path = System.IO.Path.Combine(
                TestContext.CurrentContext.TestDirectory, "TranslationResources", "DeeplLanguages.json");

            var languages = JsonConvert.DeserializeObject<List<TranslatorLanguage>>(
                System.IO.File.ReadAllText(path));

            TestContext.Out.WriteLine($"{languages.Count} languages");
            TestContext.Out.WriteLine(string.Join(" ", languages.Select(x => x.LanguageCode)));

            Assert.That(languages, Is.Not.Empty);
        }
    }
}
