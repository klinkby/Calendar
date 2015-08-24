using System.Collections.Generic;

namespace Klinkby.Calendar
{
    /// <summary>Determines how an event is positioned within a collection of existing events.</summary>
    public interface IEventPosition
    {
        /// <summary>If non-null the tested event starts exactly when this one ends.</summary>
        IEvent StartsAdjacentTo { get; }

        /// <summary>If non-null the tested event ends exactly when this one starts.</summary>
        IEvent EndsAdjacentTo { get; }
        
        /// <summary>If non-null the tested event overlaps the this one's start (but not it's end).</summary>
        IEvent OverlapsStartOf { get; }

        /// <summary>If non-null the tested event overlaps the this one's end (but not it's start).</summary>
        IEvent OverlapsEndOf { get; }

        /// <summary>Collection of events that the tested event completely overlaps.</summary>
        IEnumerable<IEvent> OverlapsCompletely { get; }
    }
}
