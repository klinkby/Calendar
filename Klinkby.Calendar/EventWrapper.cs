using System;
using System.Globalization;

namespace Klinkby.Calendar
{
    internal struct EventWrapper : IEquatable<EventWrapper>
    {
        readonly IEvent _e;
        readonly DateTime _endTime;

        internal static EventWrapper Empty = new EventWrapper();
        public override bool Equals(object obj)
        {
            if (!(obj is EventWrapper)) return false;
            var other = (EventWrapper)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            if (default(DateTime) == _endTime) return 0;
            return _e.Start.GetHashCode() ^ _e.Duration.GetHashCode();
        }

        public override string ToString()
        {
            if (default(DateTime) == _endTime) return base.ToString();
            return string.Format(CultureInfo.InvariantCulture, "{0} - {1}", _e.Start, _endTime);
        }

        internal IEvent Event
        {
            get { return _e; }
        }

        internal DateTime EndTime
        {
            get { return _endTime; }
        }

        public EventWrapper(IEvent e)
        {
            _e = e;
            _endTime = e.Start + e.Duration;
        }

        internal bool StartOverlappedBy(EventWrapper b)
        {
            return b._e.Start < _e.Start && b._endTime > _e.Start;
        }

        internal bool EndOverlappedBy(EventWrapper b)
        {
            return b._e.Start < _endTime && b._endTime > _endTime;
        }

        internal bool CompletelyOverlappedBy(EventWrapper b)
        {
            return _e.Start >= b._e.Start && _endTime <= b._endTime;
        }

        internal bool StartsAdjacentTo(EventWrapper b)
        {
            return b._endTime == _e.Start;
        }

        internal bool EndsAdjacentTo(EventWrapper b)
        {
            return _endTime == b._e.Start;
        }

        internal bool IsWayBefore(EventWrapper b)
        {
            return _endTime < b._e.Start;
        }

        internal bool IsWayAfter(EventWrapper b)
        {
            return _e.Start > b._endTime;
        }

        public bool Equals(EventWrapper other)
        {
            if (null == _e && null == other._e) return true;
            if (null == _e || null == other._e) return false;
            return _e.Start == other._e.Start && _endTime == other._endTime;
        }

        public static bool operator == (EventWrapper a, EventWrapper b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(EventWrapper a, EventWrapper b)
        {
            return !(a == b);
        }

        public bool IsEmpty()
        {
            return default(DateTime) == _endTime;
        }
    }
}
