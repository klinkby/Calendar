using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klinkby.Calendar.Test
{
    using Mocks;

    [TestClass]
    public class RemoveSlot
    {
        [TestMethod]
        public void OverlapsCompletely()
        {
            var events = AddSlot.CreateEvents();
            var slots = new SlotEvents(events);
            var removingX = events[4];
            var commands = slots.RemoveSlot(removingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(events[4], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Delete, commands[0].Verb);
        }

        [TestMethod]
        public void OverlapsEndOf()
        {
            var events = AddSlot.CreateEvents();
            var slots = new SlotEvents(events);
            var removingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 0, 30, 0), Duration = TimeSpan.FromHours(1) };
            var commands = slots.RemoveSlot(removingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(events[0], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[0].Verb);
            Assert.AreEqual(removingX.Start, events[0].End);
        }

        [TestMethod]
        public void OverlapsStartOf()
        {
            var events = AddSlot.CreateEvents();
            var slots = new SlotEvents(events);
            var bEnd = events[1].End;
            var removingX = new TestEvent("X") { Start = new DateTime(2015, 1, 1, 1, 0, 0), Duration = TimeSpan.FromHours(1) };
            var commands = slots.RemoveSlot(removingX).ToArray();
            Assert.AreEqual(1, commands.Length);
            Assert.AreEqual(events[1], commands[0].Event);
            Assert.AreEqual(DataCommandVerb.Update, commands[0].Verb);
            Assert.AreEqual(removingX.End, events[1].Start);
            Assert.AreEqual(bEnd, events[1].End);
        }
    }
}
