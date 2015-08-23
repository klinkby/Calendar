using System;

namespace Klinkby.Calendar.Test.Mocks
{
    internal class TestEvent : IEvent
    {
        public TestEvent(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public TimeSpan Duration
        {
            get;
            set;
        }

        public DateTime Start
        {
            get;
            set;
        }

        public DateTime End
        {
            get { return Start + Duration; }
        }

        public override string ToString()
        {
            return Name;
        }

    }
}
