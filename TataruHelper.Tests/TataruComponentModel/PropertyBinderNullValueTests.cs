using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using FFXIVTataruHelper;
using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.TataruComponentModel;

using NUnit.Framework;

namespace TataruHelper.Tests.TataruComponentModel
{
    /// <summary>
    /// The binder mirrors a property from one object onto the other whenever it
    /// changes. A property that has not been set yet is null, which is ordinary
    /// early in a window's life - and the binder threw on it, twice over: it
    /// compared the values by calling Equals on the source, and it asked the
    /// source for its type for a variable nothing read.
    ///
    /// Neither throw reached the user. They were collected by the event and
    /// written to the log, so the property simply did not get mirrored and the
    /// only sign was a stack trace in a file we ask people to send us.
    /// </summary>
    [TestFixture]
    public class PropertyBinderNullValueTests
    {
        [Test]
        public async Task ANullSource_IsMirroredInsteadOfThrowing()
        {
            var source = new Bindable { Value = null };
            var target = new Bindable { Value = "something" };
            Bind(source, target);

            var failures = await source.RaiseChangeAsync(nameof(Bindable.Value));

            Assert.That(failures, Is.Empty, "mirroring a null raised");
            Assert.That(target.Value, Is.Null, "the null should have been copied across");
        }

        [Test]
        public async Task ANullTarget_TakesTheValue()
        {
            var source = new Bindable { Value = "Добро пожаловать." };
            var target = new Bindable { Value = null };
            Bind(source, target);

            var failures = await source.RaiseChangeAsync(nameof(Bindable.Value));

            Assert.That(failures, Is.Empty);
            Assert.That(target.Value, Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public async Task TwoNulls_AreLeftAlone()
        {
            var source = new Bindable { Value = null };
            var target = new Bindable { Value = null };
            Bind(source, target);

            var failures = await source.RaiseChangeAsync(nameof(Bindable.Value));

            Assert.That(failures, Is.Empty);
            Assert.That(target.Value, Is.Null);
        }

        [Test]
        public async Task AnOrdinaryChange_StillMirrors()
        {
            var source = new Bindable { Value = "after" };
            var target = new Bindable { Value = "before" };
            Bind(source, target);

            var failures = await source.RaiseChangeAsync(nameof(Bindable.Value));

            Assert.That(failures, Is.Empty);
            Assert.That(target.Value, Is.EqualTo("after"));
        }

        private static void Bind(Bindable source, Bindable target)
        {
            var binder = new PropertyBinder(source, target);
            binder.AddPropertyCouple(
                new PropertyCouple<string, string>(nameof(Bindable.Value), nameof(Bindable.Value)));
        }

        /// <summary>
        /// Stands in for a settings object or a window view model: one property,
        /// and the async change notification the binder listens to. Handler
        /// failures are collected rather than thrown, exactly as the real event
        /// does, so a test can see the ones that used to be swallowed.
        /// </summary>
        private sealed class Bindable : INotifyPropertyChangedAsync
        {
            private readonly AsyncEvent<AsyncPropertyChangedEventArgs> _changed;
            private readonly List<Exception> _failures = new List<Exception>();

            public Bindable()
            {
                _changed = new AsyncEvent<AsyncPropertyChangedEventArgs>(
                    (_, ex) => _failures.Add(ex), "Bindable");
            }

            public string Value { get; set; }

            public event AsyncEventHandler<AsyncPropertyChangedEventArgs> AsyncPropertyChanged
            {
                add => _changed.Register(value);
                remove => _changed.Unregister(value);
            }

            public async Task<IReadOnlyList<Exception>> RaiseChangeAsync(string propertyName)
            {
                _failures.Clear();
                await _changed.InvokeAsync(new AsyncPropertyChangedEventArgs(this, propertyName));
                return _failures;
            }
        }
    }
}
