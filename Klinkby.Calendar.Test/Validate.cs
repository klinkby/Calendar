using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Klinkby.Calendar.Test
{
    using Mocks;

    [TestClass]
    public class Validate
    {
        [TestMethod]
        public void AllOk()
        {
            var events = AddSlot.CreateEvents();
            events.Validate();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void OutOfOrder()
        {
            var events = new[]
            {
                new TestEvent("A") { Start = new DateTime(2015, 1, 1, 0, 0, 0), Duration = TimeSpan.FromHours(1) }, // A
                new TestEvent("B") { Start = new DateTime(2015, 1, 1, 1, 30, 0), Duration = TimeSpan.FromHours(2) }, // B OVERLAPS!!!
                new TestEvent("C") { Start = new DateTime(2015, 1, 1, 3, 0, 0), Duration = TimeSpan.FromHours(1) }, // C
                new TestEvent("D") { Start = new DateTime(2015, 1, 1, 5, 0, 0), Duration = TimeSpan.FromHours(1) }, // D
                new TestEvent("E") { Start = new DateTime(2015, 1, 1, 7, 0, 0), Duration = TimeSpan.FromHours(1) } // E
            };
            events.Validate();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void Adjacent()
        {
            var events = new[]
            {
                new TestEvent("A") { Start = new DateTime(2015, 1, 1, 0, 0, 0), Duration = TimeSpan.FromHours(1) }, // A
                new TestEvent("B") { Start = new DateTime(2015, 1, 1, 1, 0, 0), Duration = TimeSpan.FromHours(1) }, // B ADJACENT!!!
                new TestEvent("C") { Start = new DateTime(2015, 1, 1, 3, 0, 0), Duration = TimeSpan.FromHours(1) }, // C
                new TestEvent("D") { Start = new DateTime(2015, 1, 1, 5, 0, 0), Duration = TimeSpan.FromHours(1) }, // D
                new TestEvent("E") { Start = new DateTime(2015, 1, 1, 7, 0, 0), Duration = TimeSpan.FromHours(1) } // E
            };
            events.Validate();
        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void Null()
        {
            var events = new[]
            {
                new TestEvent("A") { Start = new DateTime(2015, 1, 1, 0, 0, 0), Duration = TimeSpan.FromHours(1) }, // A
                null, // B null
                new TestEvent("C") { Start = new DateTime(2015, 1, 1, 3, 0, 0), Duration = TimeSpan.FromHours(1) }, // C
                new TestEvent("D") { Start = new DateTime(2015, 1, 1, 5, 0, 0), Duration = TimeSpan.FromHours(1) }, // D
                new TestEvent("E") { Start = new DateTime(2015, 1, 1, 7, 0, 0), Duration = TimeSpan.FromHours(1) } // E
            };
            events.Validate();
        }
    }
}
