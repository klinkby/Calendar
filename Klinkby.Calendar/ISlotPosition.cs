using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klinkby.Calendar
{
    public interface ISlotPosition
    {
        IEvent StartsAdjacentTo { get; }
        IEvent EndsAdjacentTo { get; }
        IEvent OverlapsStartOf { get; }
        IEvent OverlapsEndOf { get; }
        IEnumerable<IEvent> OverlapsCompletely { get; }
    }
}
