namespace Klinkby.Calendar
{
    /// <summary>Basic implementation of an <see cref="IDataCommand"/></summary>
    internal sealed class DataCommand : IDataCommand
    {
        readonly IEvent _event;
        readonly DataCommandVerb _verb;

        internal DataCommand(IEvent e, DataCommandVerb verb)
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
