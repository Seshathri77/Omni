using Microsoft.Extensions.Logging;

namespace OmniFlow.Observability;

/// <summary>
/// Structured logging helpers for consistent, searchable log messages across OmniFlow.
/// Follows production logging best practices with semantic properties and standardized formats.
/// </summary>
public static class StructuredLogging
{
    /// <summary>
    /// Logs saga lifecycle events with structured properties.
    /// </summary>
    public static class Saga
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _sagaStarted =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(1001, "SagaStarted"),
                "Saga {SagaType} started with ID {SagaId} for correlation {CorrelationId}");

        private static readonly Action<ILogger, string, string, double, Exception?> _sagaCompleted =
            LoggerMessage.Define<string, string, double>(
                LogLevel.Information,
                new EventId(1002, "SagaCompleted"),
                "Saga {SagaType} {SagaId} completed successfully in {Duration}ms");

        private static readonly Action<ILogger, string, string, double, Exception?> _sagaCompensated =
            LoggerMessage.Define<string, string, double>(
                LogLevel.Warning,
                new EventId(1003, "SagaCompensated"),
                "Saga {SagaType} {SagaId} compensated after {Duration}ms");

        private static readonly Action<ILogger, string, string, double, string, Exception?> _sagaFailed =
            LoggerMessage.Define<string, string, double, string>(
                LogLevel.Error,
                new EventId(1004, "SagaFailed"),
                "Saga {SagaType} {SagaId} failed after {Duration}ms with error: {ErrorReason}");

        private static readonly Action<ILogger, string, string, string, Exception?> _sagaStateTransition =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Debug,
                new EventId(1005, "SagaStateTransition"),
                "Saga {SagaId} transitioned from {FromStatus} to {ToStatus}");

        public static void Started(ILogger logger, string sagaType, string sagaId, string correlationId) =>
            _sagaStarted(logger, sagaType, sagaId, correlationId, null);

        public static void Completed(ILogger logger, string sagaType, string sagaId, double durationMs) =>
            _sagaCompleted(logger, sagaType, sagaId, durationMs, null);

        public static void Compensated(ILogger logger, string sagaType, string sagaId, double durationMs) =>
            _sagaCompensated(logger, sagaType, sagaId, durationMs, null);

        public static void Failed(ILogger logger, string sagaType, string sagaId, double durationMs, string errorReason, Exception? exception = null) =>
            _sagaFailed(logger, sagaType, sagaId, durationMs, errorReason, exception);

        public static void StateTransition(ILogger logger, string sagaId, string fromStatus, string toStatus) =>
            _sagaStateTransition(logger, sagaId, fromStatus, toStatus, null);
    }

    /// <summary>
    /// Logs message bus events with structured properties.
    /// </summary>
    public static class MessageBus
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _messagePublished =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Debug,
                new EventId(2001, "MessagePublished"),
                "Published message {MessageType} with ID {MessageId} for correlation {CorrelationId}");

        private static readonly Action<ILogger, string, string, double, Exception?> _messageProcessed =
            LoggerMessage.Define<string, string, double>(
                LogLevel.Information,
                new EventId(2002, "MessageProcessed"),
                "Processed message {MessageType} {MessageId} in {Duration}ms");

        private static readonly Action<ILogger, string, string, int, Exception?> _messageRetried =
            LoggerMessage.Define<string, string, int>(
                LogLevel.Warning,
                new EventId(2003, "MessageRetried"),
                "Retrying message {MessageType} {MessageId} (attempt {Attempt})");

        private static readonly Action<ILogger, string, string, string, Exception?> _messageFailed =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Error,
                new EventId(2004, "MessageFailed"),
                "Message {MessageType} {MessageId} failed: {ErrorReason}");

        private static readonly Action<ILogger, string, Exception?> _circuitBreakerOpened =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(2005, "CircuitBreakerOpened"),
                "Circuit breaker opened for {MessageType}");

        private static readonly Action<ILogger, string, string, Exception?> _duplicateMessageDetected =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(2006, "DuplicateMessageDetected"),
                "Duplicate message detected: {MessageType} {MessageId}");

        public static void Published(ILogger logger, string messageType, string messageId, string correlationId) =>
            _messagePublished(logger, messageType, messageId, correlationId, null);

        public static void Processed(ILogger logger, string messageType, string messageId, double durationMs) =>
            _messageProcessed(logger, messageType, messageId, durationMs, null);

        public static void Retried(ILogger logger, string messageType, string messageId, int attempt) =>
            _messageRetried(logger, messageType, messageId, attempt, null);

        public static void Failed(ILogger logger, string messageType, string messageId, string errorReason, Exception? exception = null) =>
            _messageFailed(logger, messageType, messageId, errorReason, exception);

        public static void CircuitBreakerOpened(ILogger logger, string messageType) =>
            _circuitBreakerOpened(logger, messageType, null);

        public static void DuplicateDetected(ILogger logger, string messageType, string messageId) =>
            _duplicateMessageDetected(logger, messageType, messageId, null);
    }

    /// <summary>
    /// Logs repository operations with structured properties.
    /// </summary>
    public static class Repository
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _operationExecuted =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Debug,
                new EventId(3001, "RepositoryOperation"),
                "Repository operation {Operation} on {EntityType} {EntityId}");

        private static readonly Action<ILogger, string, string, Exception?> _concurrencyConflict =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(3002, "ConcurrencyConflict"),
                "Optimistic concurrency conflict on {EntityType} {EntityId}");

        private static readonly Action<ILogger, string, string, string, Exception?> _operationFailed =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Error,
                new EventId(3003, "RepositoryOperationFailed"),
                "Repository operation {Operation} failed on {EntityType}: {ErrorReason}");

        public static void OperationExecuted(ILogger logger, string operation, string entityType, string entityId) =>
            _operationExecuted(logger, operation, entityType, entityId, null);

        public static void ConcurrencyConflict(ILogger logger, string entityType, string entityId) =>
            _concurrencyConflict(logger, entityType, entityId, null);

        public static void OperationFailed(ILogger logger, string operation, string entityType, string errorReason, Exception? exception = null) =>
            _operationFailed(logger, operation, entityType, errorReason, exception);
    }

    /// <summary>
    /// Logs health check events.
    /// </summary>
    public static class HealthCheck
    {
        private static readonly Action<ILogger, string, string, Exception?> _healthCheckExecuted =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(4001, "HealthCheckExecuted"),
                "Health check {HealthCheckName} completed with status {Status}");

        private static readonly Action<ILogger, string, string, Exception?> _healthCheckFailed =
            LoggerMessage.Define<string, string>(
                LogLevel.Error,
                new EventId(4002, "HealthCheckFailed"),
                "Health check {HealthCheckName} failed: {ErrorReason}");

        public static void Executed(ILogger logger, string healthCheckName, string status) =>
            _healthCheckExecuted(logger, healthCheckName, status, null);

        public static void Failed(ILogger logger, string healthCheckName, string errorReason, Exception? exception = null) =>
            _healthCheckFailed(logger, healthCheckName, errorReason, exception);
    }
}
