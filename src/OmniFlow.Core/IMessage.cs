namespace OmniFlow.Core;

/// <summary>
/// Base interface for all messages in the system.
/// </summary>
public interface IMessage
{
}

/// <summary>
/// Marker interface for commands (intent to change state).
/// </summary>
public interface ICommand : IMessage
{
}

/// <summary>
/// Marker interface for events (fact that something happened).
/// </summary>
public interface IEvent : IMessage
{
}

/// <summary>
/// Marker interface for queries (request for data).
/// </summary>
public interface IQuery : IMessage
{
}
