using System;

namespace Klinkby.Calendar
{
    /// <summary>Wraps an <see cref="IEvent"/> for simple comparison.</summary>
    internal struct EventWrapper
    {
        internal readonly IEvent Event;
        private readonly DateTime _endTime;

        internal EventWrapper(IEvent e)
        {
            Event = e;
            _endTime = e.Start + e.Duration;
        }

        internal bool StartOverlappedBy(EventWrapper b)
        {
            return b.Event.Start < Event.Start && b._endTime > Event.Start;
        }

        internal bool EndOverlappedBy(EventWrapper b)
        {
            return b.Event.Start < _endTime && b._endTime > _endTime;
        }

        internal bool CompletelyOverlappedBy(EventWrapper b)
        {
            return Event.Start >= b.Event.Start && _endTime <= b._endTime;
        }

        internal bool StartsAdjacentTo(EventWrapper b)
        {
            return b._endTime == Event.Start;
        }

        internal bool EndsAdjacentTo(EventWrapper b)
        {
            return _endTime == b.Event.Start;
        }

        internal bool IsWayBefore(EventWrapper b)
        {
            return _endTime < b.Event.Start;
        }

        internal bool IsWayAfter(EventWrapper b)
        {
            return Event.Start > b._endTime;
        }

    }
}
