using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klinkby.Calendar
{
    internal class DataCommand : IDataCommand
    {
        readonly IEvent _event;
        readonly DataCommandVerb _verb;

        public DataCommand(IEvent e, DataCommandVerb verb)
        {
            _event = e;
            _verb = verb;    
        }

        public IEvent Event
        {
            get { return _event; }
        }

        public DataCommandVerb Verb
        {
            get { return _verb; }
        }
    }
}
