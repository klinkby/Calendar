namespace Klinkby.Calendar
{
    /// <summary>Define a changes to an  event in the data layer.</summary>
    /// <seealso cref="EnumerableIEventsExtensions.AddEvent{T}(System.Collections.Generic.IEnumerable{T}, IEvent)"/>
    /// <seealso cref="EnumerableIEventsExtensions.RemoveEvent{T}(System.Collections.Generic.IEnumerable{T}, IEvent)"/>
    public interface IDataCommand
    {
        /// <summary>Event to change</summary>
        IEvent Event { get; }
        /// <summary>The command to apply to the event</summary>
        DataCommandVerb Verb { get; }
    }
}
