using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Updater.Tests
{
    [TestFixture]
    public class AsyncEventTests
    {
        [Test]
        public void Register_NullHandler_Throws()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");

            Assert.That(() => asyncEvent.Register(null), Throws.ArgumentNullException);
        }

        [Test]
        public void Unregister_NullHandler_Throws()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");

            Assert.That(() => asyncEvent.Unregister(null), Throws.ArgumentNullException);
        }

        [Test]
        public void Register_And_Unregister_UpdateHandlersCount()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");
            AsyncEventHandler handler = () => Task.CompletedTask;

            asyncEvent.Register(handler);
            Assert.That(asyncEvent.HandlersCount, Is.EqualTo(1));

            asyncEvent.Unregister(handler);
            Assert.That(asyncEvent.HandlersCount, Is.EqualTo(0));
        }

        [Test]
        public async Task InvokeAsync_NoHandlers_DoesNotThrow()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");

            await asyncEvent.InvokeAsync();
        }

        [Test]
        public async Task InvokeAsync_InvokesAllHandlers()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");
            var calls = 0;

            asyncEvent.Register(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });
            asyncEvent.Register(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; });

            await asyncEvent.InvokeAsync();

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public async Task InvokeAsync_HandlerThrows_RemainingHandlersStillRun_AndErrorHandlerCalled()
        {
            string reportedEventName = null;
            Exception reportedException = null;
            var asyncEvent = new AsyncEvent(
                (name, ex) => { reportedEventName = name; reportedException = ex; },
                "TestEvent");

            var secondHandlerRan = false;
            asyncEvent.Register(() => throw new InvalidOperationException("boom"));
            asyncEvent.Register(() => { secondHandlerRan = true; return Task.CompletedTask; });

            await asyncEvent.InvokeAsync();

            Assert.That(secondHandlerRan, Is.True);
            Assert.That(reportedEventName, Is.EqualTo("TestEvent"));
            Assert.That(reportedException, Is.InstanceOf<AggregateException>());

            var aggregate = (AggregateException)reportedException;
            Assert.That(aggregate.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(aggregate.InnerExceptions[0], Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task InvokeAsync_MultipleHandlersThrow_AllExceptionsAggregated()
        {
            Exception reportedException = null;
            var asyncEvent = new AsyncEvent((name, ex) => reportedException = ex, "TestEvent");

            asyncEvent.Register(() => throw new InvalidOperationException("first"));
            asyncEvent.Register(() => throw new ArgumentException("second"));

            await asyncEvent.InvokeAsync();

            var aggregate = (AggregateException)reportedException;
            Assert.That(aggregate.InnerExceptions, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task InvokeAsync_NoExceptions_ErrorHandlerNotCalled()
        {
            var errorHandlerCalled = false;
            var asyncEvent = new AsyncEvent((name, ex) => errorHandlerCalled = true, "TestEvent");

            asyncEvent.Register(() => Task.CompletedTask);

            await asyncEvent.InvokeAsync();

            Assert.That(errorHandlerCalled, Is.False);
        }

        [Test]
        public async Task InvokeAsync_AwaitsAsynchronousHandlers()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");
            var completed = false;

            asyncEvent.Register(async () =>
            {
                await Task.Delay(10);
                completed = true;
            });

            await asyncEvent.InvokeAsync();

            Assert.That(completed, Is.True);
        }

        [Test]
        public void ConcurrentRegisterUnregisterAndInvoke_DoesNotThrow()
        {
            var asyncEvent = new AsyncEvent((name, ex) => { }, "TestEvent");

            Assert.That(() =>
            {
                var tasks = new List<Task>();
                for (var i = 0; i < 20; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        AsyncEventHandler handler = () => Task.CompletedTask;
                        for (var j = 0; j < 100; j++)
                        {
                            asyncEvent.Register(handler);
                            asyncEvent.Unregister(handler);
                        }
                    }));
                    tasks.Add(Task.Run(async () =>
                    {
                        for (var j = 0; j < 100; j++)
                            await asyncEvent.InvokeAsync();
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }, Throws.Nothing);
        }
    }

    [TestFixture]
    public class GenericAsyncEventTests
    {
        sealed class TestEventArgs : EventArgs
        {
            public int Value { get; set; }
        }

        [Test]
        public void Register_NullHandler_Throws()
        {
            var asyncEvent = new AsyncEvent<TestEventArgs>((name, ex) => { }, "TestEvent");

            Assert.That(() => asyncEvent.Register(null), Throws.ArgumentNullException);
        }

        [Test]
        public async Task InvokeAsync_PassesEventArgsToHandlers()
        {
            var asyncEvent = new AsyncEvent<TestEventArgs>((name, ex) => { }, "TestEvent");
            var receivedValues = new List<int>();

            asyncEvent.Register(e => { receivedValues.Add(e.Value); return Task.CompletedTask; });
            asyncEvent.Register(e => { receivedValues.Add(e.Value); return Task.CompletedTask; });

            await asyncEvent.InvokeAsync(new TestEventArgs { Value = 42 });

            Assert.That(receivedValues, Is.EqualTo(new[] { 42, 42 }));
        }

        [Test]
        public async Task InvokeAsync_HandlerThrows_ErrorHandlerReceivesAggregateException()
        {
            Exception reportedException = null;
            var asyncEvent = new AsyncEvent<TestEventArgs>((name, ex) => reportedException = ex, "TestEvent");

            asyncEvent.Register(e => throw new InvalidOperationException("boom"));

            await asyncEvent.InvokeAsync(new TestEventArgs());

            Assert.That(reportedException, Is.InstanceOf<AggregateException>());
            Assert.That(
                ((AggregateException)reportedException).InnerExceptions.Single(),
                Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void Unregister_RemovesHandler()
        {
            var asyncEvent = new AsyncEvent<TestEventArgs>((name, ex) => { }, "TestEvent");
            AsyncEventHandler<TestEventArgs> handler = e => Task.CompletedTask;

            asyncEvent.Register(handler);
            asyncEvent.Unregister(handler);

            Assert.That(asyncEvent.HandlersCount, Is.EqualTo(0));
        }
    }
}
