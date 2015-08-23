using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klinkby.Calendar
{
    public class SlotEvents
    {
        readonly IEnumerable<IEvent> _sortedEvents;

        public SlotEvents(IEnumerable<IEvent> sortedEvents) 
        {
            if (null == sortedEvents) throw new ArgumentNullException("sortedEvents");
            _sortedEvents = sortedEvents;
        }

        public IEnumerable<IEvent> SortedEvents
        {
            get
            {
                return _sortedEvents;
            }
        }

        public IEnumerable<IDataCommand> AddSlot(IEvent evt)
        {
            var position = ResolveSlotPosition(evt);
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

        public IEnumerable<IDataCommand> RemoveSlot(IEvent evt)
        {
            var position = ResolveSlotPosition(evt);
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
        /// Does a sanity check on the SortedEvents property. Throws InvalidOperationException if it is not ok.
        /// 
        /// </summary>
        public void Validate()
        {
            DateTime lastEnd = DateTime.MinValue;
            int i = 0;
            foreach (var item in _sortedEvents)
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

        public ISlotPosition ResolveSlotPosition(IEvent newEvent)
        {
            var newItem = new EventWrapper(newEvent);
            var result = new SlotPosition();
            using (var enumerator = SortedEvents.GetEnumerator())
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