using System;

namespace Klinkby.Calendar
{
    /// <summary>An event that has a duration.</summary>
    /// <remarks>.NET DateTime does not handle time zones, so use with care e.g. always use UTC.</remarks>
    public interface IEvent
    {
        /// <summary>Event start time</summary>
        DateTime Start { get; set; }
        /// <summary>Event duration (must be positive)</summary>
        TimeSpan Duration { get; set; }
    }
}