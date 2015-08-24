using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Klinkby.Calendar
{
    /// <summary>Extension methods for an ascending ordered <see cref="T:IEnumerable`1"/> 
    /// using <see cref="IEvent"/> as generic type parameter.</summary>
    public static class EnumerableIEventsExtensions
    {
        /// <summary>
        /// Get the commands needed for inserting a new time slot into an existing ascending ordered collection.
        /// Will join adjacent events and prevent overlapping.
        /// </summary>
        /// <typeparam name="T"><see cref="IEvent"/> implementation</typeparam>
        /// <param name="orderedEvents">Collection of ascending ordered events.</param>
        /// <param name="evt">Event to add</param>
        /// <returns>Collection of <see cref="DataCommand"/>.</returns>
        public static IEnumerable<IDataCommand> AddEvent<T>(this IEnumerable<T> orderedEvents, IEvent evt)
            where T: IEvent
        {
            var position = ResolvePosition(orderedEvents, evt);
            // does the new slot conver entire slots (C in Y)
            // tested by OverlapsCompletely()
            foreach (var e in position.OverlapsCompletely)
            {
                yield return new DataCommand(e, DataCommandVerb.Delete);
            }
            IEvent extendEnd = position.StartsAdjacentTo ?? position.OverlapsEndOf;
            IEvent prependStart = position.EndsAdjacentTo ?? position.OverlapsStartOf;
            // does the new slot fill the entire space between two other slots (B, C)
            // tested by MergeBC()
            if (null != extendEnd && null != prependStart)
            {
                extendEnd.Duration = (prependStart.Start + prependStart.Duration) 
                    - extendEnd.Start;
                Debug.Assert(extendEnd.Duration.Ticks > 0);
                yield return new DataCommand(prependStart, DataCommandVerb.Delete);
                yield return new DataCommand(extendEnd, DataCommandVerb.Update);
                yield break;
            }
            // does the new slot extend the end time of a preceding slot (B)
            // tested by ExtendBEnd()
            if (null != extendEnd)
            {
                extendEnd.Duration = (evt.Start + evt.Duration) - extendEnd.Start;
                Debug.Assert(extendEnd.Duration.Ticks > 0);
                yield return new DataCommand(extendEnd, DataCommandVerb.Update);
                yield break;
            }
            // does the new slot prepend the start time of a following slot (C)
            // tested by ExtendCStartAdjacent()
            if (null != prependStart)
            {
                prependStart.Duration = (prependStart.Start + prependStart.Duration) - evt.Start;
                Debug.Assert(prependStart.Duration.Ticks > 0);
                prependStart.Start = evt.Start;
                yield return new DataCommand(prependStart, DataCommandVerb.Update);
                yield break;
            }
            // this is completely new territory
            // tested by AddNew()
            yield return new DataCommand(evt, DataCommandVerb.Insert);
        }

        /// <summary>
        /// Get the commands needed for removing time slot from an existing ascending ordered collection.
        /// Will clip existing events if they overlap the given time slot.
        /// </summary>
        /// <typeparam name="T"><see cref="IEvent"/> implementation</typeparam>
        /// <param name="orderedEvents">Collection of ascending ordered events.</param>
        /// <param name="evt">Time slot to remove</param>
        /// <returns>Collection of <see cref="DataCommand"/>.</returns>
        public static IEnumerable<IDataCommand> RemoveEvent<T>(this IEnumerable<T> orderedEvents, IEvent evt)
            where T : IEvent
        {
            var position = ResolvePosition(orderedEvents, evt);
            var overlapsEndOf = position.OverlapsEndOf;
            var overlapsStartOf = position.OverlapsStartOf;
            // does this slot conver entire slots (C in Y)
            // tested by OverlapsCompletely()
            foreach (var e in position.OverlapsCompletely)
            {
                yield return new DataCommand(e, DataCommandVerb.Delete);
            }
            // does this slot overlap the end time of a preceding slot (B)
            // tested by OverlapsEndOf()
            if (null != overlapsEndOf)
            {
                overlapsEndOf.Duration = evt.Start - overlapsEndOf.Start;
                Debug.Assert(overlapsEndOf.Duration.Ticks > 0);
                yield return new DataCommand(overlapsEndOf, DataCommandVerb.Update);
            }
            // does the new slot overlap the start time of a following slot (C)
            // tested by OverlapsStartOf()
            if (null != overlapsStartOf)
            {
                overlapsStartOf.Duration = (overlapsStartOf.Start + overlapsStartOf.Duration)- (evt.Start + evt.Duration);
                Debug.Assert(overlapsStartOf.Duration.Ticks > 0);
                overlapsStartOf.Start = evt.Start + evt.Duration;
                yield return new DataCommand(overlapsStartOf, DataCommandVerb.Update);
                yield break;
            }
        }

        /// <summary>
        /// Does a sanity check on the events to ensure none are null, have positive durations, are ordered ascending and does not overlap.
        /// Throws on first error found.
        /// </summary>
        /// <typeparam name="T"><see cref="IEvent"/> implementation</typeparam>
        /// <param name="orderedEvents">Collection of ascending ordered events.</param>
        /// <exception cref="InvalidOperationException">Thrown if there is an issue with the event list.</exception>
        public static void Validate<T>(this IEnumerable<T> orderedEvents)
            where T: IEvent
        {
            if (null == orderedEvents) throw new ArgumentNullException("orderedEvents");
            DateTime lastEnd = DateTime.MinValue;
            int i = 0;
            foreach (var item in orderedEvents)
            {
                if (null == item)
                {
                    throw new InvalidOperationException("Event index " + i + " is null");
                }
                if (0 >= item.Duration.Ticks)
                {
                    throw new InvalidOperationException("Event index " + i + " starting at " + item.Start + " ends in the past");
                }
                if (item.Start <= lastEnd)
                {
                    throw new InvalidOperationException("Event index " + i + " starts at " + item.Start + " before or when previous event ends at " + lastEnd);
                }
                i++;
                lastEnd = item.Start + item.Duration;
            }
        }

        /// <summary>
        /// Enumerates the orderedEvents to determine how evt fits in.
        /// </summary>
        /// <typeparam name="T"><see cref="IEvent"/> implementation</typeparam>
        /// <param name="orderedEvents">Collection of ascending ordered events.</param>
        /// <param name="evt">The time slot to fit in.</param>
        /// <returns>See <see cref="IEventPosition"/></returns>
        public static IEventPosition ResolvePosition<T>(this IEnumerable<T> orderedEvents, IEvent evt)
            where T: IEvent
        {
            if (null == orderedEvents) throw new ArgumentNullException("orderedEvents");
            if (null == evt) throw new ArgumentNullException("newEvent");
            var newItem = new EventWrapper(evt);
            var result = new EventPosition();
            using (var enumerator = orderedEvents.GetEnumerator())
            {
                /*

                    |  A   |%|   B   |%%%%%%%|   C   |%|   D  |%%%%%%%%%|  E  |
                                   |<--- x --->|
                                     |<-------- y -------->|
                                                                |<- z ->|
                */
                EventWrapper listItem;
                // skip preceding past events (A)
                do
                {
                    if (!enumerator.MoveNext()) return result;
                    listItem = new EventWrapper(enumerator.Current);
                } while (listItem.IsWayBefore(newItem));
                // does newItem start adjacent (Y to B)
                if (newItem.StartsAdjacentTo(listItem))
                {
                    result.StartsAdjacentTo = listItem.Event;
                    if (!enumerator.MoveNext()) return result;
                    listItem = new EventWrapper(enumerator.Current);
                }
                // or overlaps the end of an event (Y on B)
                else if (listItem.EndOverlappedBy(newItem))
                {
                    result.OverlapsEndOf = listItem.Event;
                    if (!enumerator.MoveNext()) return result;
                    listItem = new EventWrapper(enumerator.Current);
                }
                // is the following events contained in this one (C in Y)
                while (listItem.CompletelyOverlappedBy(newItem))
                {
                    result.OverlapsCompletely.Enqueue(listItem.Event);
                    if (!enumerator.MoveNext()) return result;
                    listItem = new EventWrapper(enumerator.Current);
                }
                // does the following event start exactly where this one ends (Y to E)
                if (newItem.EndsAdjacentTo(listItem))
                {
                    result.EndsAdjacentTo = listItem.Event;
                }
                // or overlaps the start of an event (Y on D)
                else if (newItem.EndOverlappedBy(listItem))
                {
                    result.OverlapsStartOf = listItem.Event;
                }
                // skip the rest
            }
            return result;
        }
    }
}