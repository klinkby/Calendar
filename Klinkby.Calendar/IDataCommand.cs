using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Klinkby.Calendar
{
    public interface IDataCommand
    {
        IEvent Event { get; }
        DataCommandVerb Verb { get; }
    }
}
