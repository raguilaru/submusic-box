using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoxSubMusic.Core.Concurrency;
using Xunit;

namespace BoxSubMusic.Core.Tests
{
    public class BoundedParallelTests
    {
        [Fact]
        public async Task Results_PreserveInputOrder()
        {
            int[] items = Enumerable.Range(0, 25).ToArray();

            // Later items finish sooner: order must still follow the input.
            int[] results = await BoundedParallel.MapAsync(
                items,
                async i =>
                {
                    await Task.Delay(25 - i);
                    return i * 10;
                },
                maxConcurrent: 4);

            Assert.Equal(items.Select(i => i * 10), results);
        }

        [Fact]
        public async Task Concurrency_NeverExceedsTheBound()
        {
            const int Bound = 3;
            int inFlight = 0;
            int peak = 0;
            object gate = new object();

            await BoundedParallel.MapAsync(
                Enumerable.Range(0, 20).ToArray(),
                async i =>
                {
                    lock (gate)
                    {
                        inFlight++;
                        peak = Math.Max(peak, inFlight);
                    }

                    await Task.Delay(10);

                    lock (gate)
                    {
                        inFlight--;
                    }

                    return i;
                },
                Bound);

            Assert.InRange(peak, 1, Bound);
        }

        [Fact]
        public async Task EmptyInput_ReturnsEmptyWithoutInvokingWorker()
        {
            bool invoked = false;
            int[] results = await BoundedParallel.MapAsync(
                new int[0],
                i =>
                {
                    invoked = true;
                    return Task.FromResult(i);
                },
                maxConcurrent: 4);

            Assert.Empty(results);
            Assert.False(invoked);
        }

        [Fact]
        public async Task WorkerFailure_PropagatesAfterStartedWorkSettles()
        {
            var completed = new List<int>();
            object gate = new object();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                BoundedParallel.MapAsync(
                    Enumerable.Range(0, 6).ToArray(),
                    async i =>
                    {
                        await Task.Delay(5);
                        if (i == 2)
                        {
                            throw new InvalidOperationException("boom");
                        }

                        lock (gate)
                        {
                            completed.Add(i);
                        }

                        return i;
                    },
                    maxConcurrent: 2));

            // The failing item never lands in the completed set.
            Assert.DoesNotContain(2, completed);
        }

        [Fact]
        public async Task BoundLargerThanInput_StillCompletes()
        {
            int[] results = await BoundedParallel.MapAsync(
                new[] { 1, 2 },
                i => Task.FromResult(i + 1),
                maxConcurrent: 16);

            Assert.Equal(new[] { 2, 3 }, results);
        }

        [Fact]
        public async Task InvalidBound_Throws()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                BoundedParallel.MapAsync(new[] { 1 }, Task.FromResult, 0));
        }
    }
}
