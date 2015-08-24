namespace Klinkby.Calendar
{
    /// <summary>Command types to manipulate events in the data layer.</summary>
    /// <seealso cref="IDataCommand"/>
    public enum DataCommandVerb
    {
        /// <summary>Create a new event</summary>
        Insert,
        /// <summary>Update an existing event</summary>
        Update,
        /// <summary>Delete an event</summary>
        Delete
    }
}
