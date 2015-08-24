using System.Collections.Generic;

namespace Klinkby.Calendar
{
    /// <summary>Basic implementation of <see cref="IEventPosition"/>.</summary>
    internal class EventPosition : IEventPosition
    {
        const int InitialQueueCapacity = 5;

        internal EventPosition()
        {
            OverlapsCompletely = new Queue<IEvent>(InitialQueueCapacity);
        }

        public IEvent EndsAdjacentTo
        {
            get;
            internal set;
        }

        IEnumerable<IEvent> IEventPosition.OverlapsCompletely
        {
            get { return OverlapsCompletely; }
        }

        internal Queue<IEvent> OverlapsCompletely
        {
            get;
            private set;  
        }

        public IEvent OverlapsEndOf
        {
            get;
            internal set;
        }

        public IEvent OverlapsStartOf
        {
            get;
            internal set;
        }

        public IEvent StartsAdjacentTo
        {
            get;
            internal set;
        }
    }
}
