using System;

namespace Klinkby.Calendar
{
    public interface IEvent
    {
        DateTime Start { get; set; }
        TimeSpan Duration { get; set; }
    }
}