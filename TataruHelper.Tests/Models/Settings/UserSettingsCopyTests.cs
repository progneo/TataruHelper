using System.Linq;
using System.Reflection;

using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests.Models.Settings
{
    // The copy constructor missed three properties and got two wrong: the
    // background colour was assigned to a local variable of the same name and
    // thrown away, and direct memory reading was set to true rather than
    // copied. Nothing uses it yet, which is the only reason none of that had
    // been noticed - it was waiting for whoever used it first.
    //
    // These walk the properties rather than listing them, so a property added
    // later and forgotten here fails the same way.
    [TestFixture]
    public class UserSettingsCopyTests
    {
        [Test]
        public void EverySimplePropertyIsCopied()
        {
            var original = new UserSettings();
            var changed = 0;

            foreach (var property in Simple())
            {
                property.SetValue(original, Other(property.GetValue(original)));
                changed++;
            }

            Assert.That(changed, Is.GreaterThan(5), "nothing to check means the walk found nothing");

            var copy = new UserSettings(original);

            Assert.Multiple(() =>
            {
                foreach (var property in Simple())
                {
                    Assert.That(property.GetValue(copy), Is.EqualTo(property.GetValue(original)), property.Name);
                }
            });
        }

        [Test]
        public void TheColoursAreCopied()
        {
            // Not simple enough for the walk above, and the one that was wrong.
            var original = new UserSettings
            {
                BackgroundColor = System.Windows.Media.Colors.Red,
                Font1Color = System.Windows.Media.Colors.Green,
                Font2Color = System.Windows.Media.Colors.Blue
            };

            var copy = new UserSettings(original);

            Assert.Multiple(() =>
            {
                Assert.That(copy.BackgroundColor, Is.EqualTo(System.Windows.Media.Colors.Red));
                Assert.That(copy.Font1Color, Is.EqualTo(System.Windows.Media.Colors.Green));
                Assert.That(copy.Font2Color, Is.EqualTo(System.Windows.Media.Colors.Blue));
            });
        }

        [Test]
        public void EveryHotkeyIsCopied()
        {
            // Clear chat was the one being left behind.
            var original = new UserSettings();
            original.ShowHideChatKeys.AddKey(System.Windows.Input.Key.F1);
            original.ClickThoughtChatKeys.AddKey(System.Windows.Input.Key.F2);
            original.ClearChatKeys.AddKey(System.Windows.Input.Key.F3);

            var copy = new UserSettings(original);

            Assert.Multiple(() =>
            {
                Assert.That(copy.ShowHideChatKeys, Is.EqualTo(original.ShowHideChatKeys));
                Assert.That(copy.ClickThoughtChatKeys, Is.EqualTo(original.ClickThoughtChatKeys));
                Assert.That(copy.ClearChatKeys, Is.EqualTo(original.ClearChatKeys), "clear chat was left behind");
            });
        }

        /// <summary>
        /// The properties whose values can be varied and compared without
        /// knowing anything about them.
        /// </summary>
        private static PropertyInfo[] Simple()
        {
            return typeof(UserSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.CanWrite)
                .Where(x => x.PropertyType == typeof(bool) ||
                            x.PropertyType == typeof(int) ||
                            x.PropertyType == typeof(string))
                .OrderBy(x => x.Name)
                .ToArray();
        }

        private static object Other(object value)
        {
            switch (value)
            {
                case bool flag: return !flag;
                case int number: return number + 7;
                case string text: return text + "-changed";
                default: return value;
            }
        }
    }
}
