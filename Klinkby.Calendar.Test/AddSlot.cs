using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klinkby.Calendar.Test
{
    using System.Collections.Generic;
    using Mocks;

    [TestClass]
    public class AddSlot
    {
        internal static IList<TestEvent> CreateEvents()
        {
            return new[]
            {
                new TestEvent("A") { Start = new DateTime(2015, 1, 1, 0, 0, 0), Duration = TimeSpan.FromHours(1) }, // A
                new TestEvent("B") { Start = new DateTime(2015, 1, 1, 1, 30, 0), Duration = TimeSpan.FromHours(1) }, // B
                new TestEvent("C") { Start = new DateTime(2015, 1, 1, 3, 0, 0), Duration = TimeSpan.FromHours(1) }, // C
                new TestEvent("D") { Start = new DateTime(2015, 1, 1, 5, 0, 0), Duration = TimeSpan.FromHours(1) }, // D
                new TestEvent("E") { Start = new DateTime(2015, 1, 1, 7, 0, 0), Duration = TimeSpan.FromHours(1) } // E
            };
        }

        [TestMethod]
        public void OverlapsCompletely()
        {
            var events = CreateEvents();
            var bStart = events[1].Start;
            DateTime dEnd = events[3].End;
            var addingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 2, 30, 0), Duration = TimeSpan.FromMinutes(180) };

            var pos = events.ResolvePosition(addingX);
            Assert.IsNull(pos.OverlapsEndOf);
            Assert.IsNotNull(pos.StartsAdjacentTo);
            Assert.IsNull(pos.EndsAdjacentTo);
            Assert.IsNotNull(pos.OverlapsStartOf);
            Assert.AreEqual(1, pos.OverlapsCompletely.Count());
            Assert.AreEqual(events[2], pos.OverlapsCompletely.First());

            var commands = events.AddEvent(addingX).ToArray();
            Assert.AreEqual(3, commands.Length);
            Assert.AreEqual(events[2], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Delete, commands[0].Verb);
            Assert.AreEqual(events[3], commands[1].Event);
            Assert.AreEqual(DataCommandVerb.Delete, commands[1].Verb);
            Assert.AreEqual(events[1], commands[2].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[2].Verb);
            Assert.AreEqual(bStart, events[1].Start);
            Assert.AreEqual(dEnd, events[1].End);
        }

        [TestMethod]
        public void MergeBC()
        {
            var events = CreateEvents();
            DateTime cEnd = events[2].End;
            var addingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 2, 30, 0), Duration = TimeSpan.FromMinutes(45) };

            var pos = events.ResolvePosition(addingX);
            Assert.AreEqual(events[1], pos.StartsAdjacentTo);
            Assert.AreEqual(events[2], pos.OverlapsStartOf);
            Assert.IsNull(pos.OverlapsEndOf);
            Assert.IsNull(pos.EndsAdjacentTo);
            Assert.IsFalse(pos.OverlapsCompletely.Any());

            var commands = events.AddEvent(addingX).ToArray();
            Assert.AreEqual(2, commands.Length);
            Assert.AreEqual(events[2], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Delete, commands[0].Verb);
            Assert.AreEqual(events[1], commands[1].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[1].Verb);
            Assert.AreEqual(cEnd, events[1].End);

        }

        [TestMethod]
        public void ExtendCStartAdjacent()
        {
            var events = CreateEvents();
            DateTime cEnd = events[2].End;
            var addingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 2, 45, 0), Duration = TimeSpan.FromMinutes(15) };

            var pos = events.ResolvePosition(addingX);
            Assert.AreEqual(events[2], pos.EndsAdjacentTo);
            Assert.IsNull(pos.OverlapsEndOf);
            Assert.IsNull(pos.StartsAdjacentTo);
            Assert.IsNull(pos.OverlapsStartOf);
            Assert.IsFalse(pos.OverlapsCompletely.Any());

            var commands = events.AddEvent(addingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(events[2], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[0].Verb);
            Assert.AreEqual(addingX.Start, events[2].Start);
            Assert.AreEqual(cEnd, events[2].End);
        }

        [TestMethod]
        public void ExtendBEnd()
        {
            var events = CreateEvents();
            var bStart = events[1].Start;
            var addingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 1, 45, 0), Duration = TimeSpan.FromHours(1) };

            var pos = events.ResolvePosition(addingX);
            Assert.AreEqual(events[1], pos.OverlapsEndOf);
            Assert.IsNull(pos.StartsAdjacentTo);
            Assert.IsNull(pos.EndsAdjacentTo);
            Assert.IsNull(pos.OverlapsStartOf);
            Assert.IsFalse(pos.OverlapsCompletely.Any());

            var commands = events.AddEvent(addingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(events[1], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[0].Verb);
            Assert.AreEqual(bStart, events[1].Start);
            Assert.AreEqual(addingX.End, events[1].End);
        }

        [TestMethod]
        public void AddNew()
        {
            var events = CreateEvents();
            var bStart = events[1].Start;
            var addingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 4, 15, 0), Duration = TimeSpan.FromMinutes(1) };

            var pos = events.ResolvePosition(addingX);
            Assert.IsNull(pos.OverlapsEndOf);
            Assert.IsNull(pos.StartsAdjacentTo);
            Assert.IsNull(pos.EndsAdjacentTo);
            Assert.IsNull(pos.OverlapsStartOf);
            Assert.IsFalse(pos.OverlapsCompletely.Any());

            var commands = events.AddEvent(addingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(addingX, commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Insert, commands[0].Verb);
        }


    }
}
