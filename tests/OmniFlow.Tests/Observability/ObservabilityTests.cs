using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Observability;
using OmniFlow.Sagas;
using OmniFlow.Tests.Sagas;
using System.Diagnostics.Metrics;
using Xunit;

namespace OmniFlow.Tests.Observability;

/// <summary>
/// Integration tests for OmniFlow metrics and observability features.
/// </summary>
public class ObservabilityTests
{
    [Fact]
    public void Metrics_Should_Record_Saga_Started()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var measurements = new List<double>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.sagas.started")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.sagas.started")
            {
                measurements.Add(measurement);
            }
        });

        listener.Start();

        // Act
        metrics.RecordSagaStarted("TestSaga");
        metrics.RecordSagaStarted("TestSaga");
        metrics.RecordSagaStarted("OrderSaga");

        // Assert
        measurements.Should().HaveCount(3);
        measurements.Sum().Should().Be(3);

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Saga_Duration()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var durations = new List<double>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.sagas.duration")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.sagas.duration")
            {
                durations.Add(measurement);
            }
        });

        listener.Start();

        // Act
        metrics.RecordSagaStarted("TestSaga");
        metrics.RecordSagaCompleted("TestSaga", 123.45);

        // Assert
        durations.Should().HaveCount(1);
        durations[0].Should().Be(123.45);

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Track_Active_Sagas()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var activeSagaCounts = new List<int>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.sagas.active")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.sagas.active")
            {
                activeSagaCounts.Add(measurement);
            }
        });

        listener.Start();
        listener.RecordObservableInstruments();

        // Initial count should be 0
        activeSagaCounts.Last().Should().Be(0);
        activeSagaCounts.Clear();

        // Act - Start sagas
        metrics.RecordSagaStarted("TestSaga");
        metrics.RecordSagaStarted("OrderSaga");
        listener.RecordObservableInstruments();

        // Assert - Last value should have 2 active sagas
        activeSagaCounts.Last().Should().Be(2);
        activeSagaCounts.Clear();

        // Act - Complete one saga
        metrics.RecordSagaCompleted("TestSaga", 100);
        listener.RecordObservableInstruments();

        // Assert - Last value should have 1 active saga
        activeSagaCounts.Last().Should().Be(1);
        activeSagaCounts.Clear();

        // Act - Compensate the other saga
        metrics.RecordSagaCompensated("OrderSaga", 150);
        listener.RecordObservableInstruments();

        // Assert - Last value should have 0 active sagas
        activeSagaCounts.Last().Should().Be(0);

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Message_Failures_With_Error_Type()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var errorTypes = new List<string>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.messages.failed")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.messages.failed")
            {
                var errorType = tags.ToArray().FirstOrDefault(t => t.Key == "error_type").Value as string;
                if (errorType != null)
                    errorTypes.Add(errorType);
            }
        });

        listener.Start();

        // Act
        metrics.RecordMessageFailed("OrderCreated", "TimeoutException");
        metrics.RecordMessageFailed("OrderCreated", "ValidationException");

        // Assert
        errorTypes.Should().HaveCount(2);
        errorTypes.Should().Contain("TimeoutException");
        errorTypes.Should().Contain("ValidationException");

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Repository_Operations()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var operations = new List<(string operation, string entityType)>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.repository.operations")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.repository.operations")
            {
                var tagArray = tags.ToArray();
                var operation = tagArray.FirstOrDefault(t => t.Key == "operation").Value as string;
                var entityType = tagArray.FirstOrDefault(t => t.Key == "entity_type").Value as string;
                if (operation != null && entityType != null)
                    operations.Add((operation, entityType));
            }
        });

        listener.Start();

        // Act
        metrics.RecordRepositoryOperation("Get", "OrderSagaState");
        metrics.RecordRepositoryOperation("Save", "OrderSagaState");
        metrics.RecordRepositoryOperation("Delete", "PaymentSagaState");

        // Assert
        operations.Should().HaveCount(3);
        operations.Should().Contain(("Get", "OrderSagaState"));
        operations.Should().Contain(("Save", "OrderSagaState"));
        operations.Should().Contain(("Delete", "PaymentSagaState"));

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Circuit_Breaker_Opens()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var circuitBreakerOpens = new List<string>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.circuit_breaker.opened")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.circuit_breaker.opened")
            {
                var messageType = tags.ToArray().FirstOrDefault(t => t.Key == "message_type").Value as string;
                if (messageType != null)
                    circuitBreakerOpens.Add(messageType);
            }
        });

        listener.Start();

        // Act
        metrics.RecordCircuitBreakerOpened("OrderCreated");
        metrics.RecordCircuitBreakerOpened("PaymentRequested");

        // Assert
        circuitBreakerOpens.Should().HaveCount(2);
        circuitBreakerOpens.Should().Contain("OrderCreated");
        circuitBreakerOpens.Should().Contain("PaymentRequested");

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Duplicate_Messages()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var duplicateCount = 0L;

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.idempotency.duplicates_detected")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.idempotency.duplicates_detected")
            {
                duplicateCount += measurement;
            }
        });

        listener.Start();

        // Act
        metrics.RecordDuplicateMessageDetected("OrderCreated");
        metrics.RecordDuplicateMessageDetected("OrderCreated");
        metrics.RecordDuplicateMessageDetected("PaymentProcessed");

        // Assert
        duplicateCount.Should().Be(3);

        listener.Dispose();
    }

    [Fact]
    public void Metrics_Should_Record_Optimistic_Concurrency_Failures()
    {
        // Arrange
        var metrics = new OmniFlowMetrics();
        var listener = new MeterListener();
        var concurrencyFailures = new List<string>();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName && 
                instrument.Name == "omniflow.repository.concurrency_failures")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.repository.concurrency_failures")
            {
                var entityType = tags.ToArray().FirstOrDefault(t => t.Key == "entity_type").Value as string;
                if (entityType != null)
                    concurrencyFailures.Add(entityType);
            }
        });

        listener.Start();

        // Act
        metrics.RecordOptimisticConcurrencyFailure("OrderSagaState");
        metrics.RecordOptimisticConcurrencyFailure("OrderSagaState");

        // Assert
        concurrencyFailures.Should().HaveCount(2);
        concurrencyFailures.Should().AllBe("OrderSagaState");

        listener.Dispose();
    }

    [Fact]
    public async Task Integration_Saga_Execution_Should_Record_Metrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOmniFlowCore();
        services.AddOmniFlowMessageBus(options => options.Provider = MessageBusProvider.InMemory);
        services.AddOmniFlowSagas();
        services.AddOmniFlowObservability("TestService");
        services.AddSaga<TestSaga, TestSagaState>();

        var serviceProvider = services.BuildServiceProvider();
        var metrics = serviceProvider.GetRequiredService<OmniFlowMetrics>();
        var repository = serviceProvider.GetRequiredService<ISagaRepository<TestSagaState>>();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        var saga = serviceProvider.GetRequiredService<TestSaga>();

        var listener = new MeterListener();
        var sagasStarted = 0L;
        var sagasCompleted = 0L;

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OmniFlowMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "omniflow.sagas.started")
                sagasStarted += measurement;
            if (instrument.Name == "omniflow.sagas.completed")
                sagasCompleted += measurement;
        });

        listener.Start();

        saga.Initialize(repository, messageBus);

        // Act
        metrics.RecordSagaStarted("TestSaga");
        await saga.StartAsync("test-correlation");
        await saga.CompleteTestAsync();
        metrics.RecordSagaCompleted("TestSaga", 100);

        // Assert
        sagasStarted.Should().Be(1);
        sagasCompleted.Should().Be(1);

        listener.Dispose();
    }
}
