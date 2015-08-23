using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klinkby.Calendar
{
    internal class SlotPosition : ISlotPosition
    {
        public SlotPosition()
        {
            OverlapsCompletely = new Queue<IEvent>(10);
        }

        public IEvent EndsAdjacentTo
        {
            get;
            internal set;
        }

        IEnumerable<IEvent> ISlotPosition.OverlapsCompletely
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
