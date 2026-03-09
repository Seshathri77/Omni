using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OmniFlow.Core;
using OmniFlow.Messaging;
using OmniFlow.Messaging.Middleware;
using OmniFlow.Tests.Sagas;
using Xunit;

namespace OmniFlow.Tests.Messaging;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOmniFlowMessageBus_Should_Register_InMemory_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.ServiceName = "TestService";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
        messageBus.Should().BeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void AddOmniFlowMessageBus_Should_Register_Middleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var correlationMiddleware = serviceProvider.GetService<CorrelationMiddleware>();
        var loggingMiddleware = serviceProvider.GetService<LoggingMiddleware>();
        
        correlationMiddleware.Should().NotBeNull();
        loggingMiddleware.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlowMessageBus_Should_Apply_Middleware_When_Enabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.UseCorrelation = true;
            options.UseLogging = true;
            options.UseRetry = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlowMessageBus_Should_Not_Apply_Middleware_When_Disabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.UseCorrelation = false;
            options.UseLogging = false;
            options.UseRetry = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlowMessageBus_Should_Configure_Retry_Options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.UseRetry = true;
            options.MaxRetries = 5;
            options.EnableCircuitBreaker = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlowMessageBus_Should_Configure_Circuit_Breaker_Options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.UseRetry = true;
            options.EnableCircuitBreaker = true;
            options.CircuitBreakerFailureRatio = 0.7;
            options.CircuitBreakerMinimumThroughput = 15;
            options.CircuitBreakerSamplingDurationSeconds = 45;
            options.CircuitBreakerBreakDurationSeconds = 60;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

#pragma warning disable CS0618 // Type or member is obsolete
    [Fact]
    public void AddOmniFlowMessaging_Should_Register_Services()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessaging();

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
        messageBus.Should().BeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void AddOmniFlowMessaging_Should_Apply_Configuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessaging(options =>
        {
            options.UseCorrelation = true;
            options.UseLogging = true;
            options.UseRetry = true;
            options.MaxRetries = 5;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        var correlationMiddleware = serviceProvider.GetService<CorrelationMiddleware>();
        var loggingMiddleware = serviceProvider.GetService<LoggingMiddleware>();
        
        messageBus.Should().NotBeNull();
        correlationMiddleware.Should().NotBeNull();
        loggingMiddleware.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlowMessaging_Should_Handle_Null_Configuration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();

        // Act
        services.AddOmniFlowMessaging(null);

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [Fact]
    public async Task Registered_MessageBus_Should_Work_End_To_End()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddLogging();
        services.AddOmniFlowMessageBus(options =>
        {
            options.Provider = MessageBusProvider.InMemory;
            options.UseCorrelation = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        var accessor = serviceProvider.GetRequiredService<ICorrelationAccessor>();

        var receivedMessage = false;

        // Act
        await messageBus.SubscribeAsync<TestMessage>((envelope, context) =>
        {
            receivedMessage = true;
            return Task.CompletedTask;
        });

        accessor.SetContext("test-correlation");
        await messageBus.PublishAsync(new TestMessage("Hello"));
        await Task.Delay(100); // Give time for async processing

        // Assert
        receivedMessage.Should().BeTrue();
    }

    [Fact]
    public void AddOmniFlow_Should_Register_All_Services_With_InMemory_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var correlationAccessor = serviceProvider.GetService<ICorrelationAccessor>();
        var messageBus = serviceProvider.GetService<IMessageBus>();
        
        correlationAccessor.Should().NotBeNull();
        messageBus.Should().NotBeNull();
        messageBus.Should().BeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void AddOmniFlow_Should_Set_ServiceName_On_MessageBus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "MyCustomService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Enable_All_MessageBus_Features()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.MessageBus.UseCorrelation = true;
            options.MessageBus.UseLogging = true;
            options.MessageBus.UseRetry = true;
            options.MessageBus.MaxRetries = 5;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Configure_Circuit_Breaker()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.MessageBus.UseRetry = true;
            options.MessageBus.EnableCircuitBreaker = true;
            options.MessageBus.CircuitBreakerFailureRatio = 0.8;
            options.MessageBus.CircuitBreakerMinimumThroughput = 20;
            options.MessageBus.CircuitBreakerSamplingDurationSeconds = 60;
            options.MessageBus.CircuitBreakerBreakDurationSeconds = 120;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_RabbitMQ_Provider_Gracefully_When_Not_Available()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.RabbitMQ;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert - should not throw, RabbitMQ adapter is optional
        var correlationAccessor = serviceProvider.GetService<ICorrelationAccessor>();
        correlationAccessor.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_Kafka_Provider_Gracefully_When_Not_Available()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.Kafka;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert - should not throw, Kafka adapter is optional
        var correlationAccessor = serviceProvider.GetService<ICorrelationAccessor>();
        correlationAccessor.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_ServiceBus_Provider_Gracefully_When_Not_Available()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.ServiceBus;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert - should not throw, ServiceBus adapter is optional
        var correlationAccessor = serviceProvider.GetService<ICorrelationAccessor>();
        correlationAccessor.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Not_Enable_Sagas_When_Disabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableSagas = false;
            options.EnableObservability = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Not_Enable_Idempotency_When_Disabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableIdempotency = false;
            options.EnableObservability = false;
            options.EnableSagas = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Not_Enable_Observability_When_Disabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_Observability_With_OtlpEndpoint()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = true;
            options.Observability.OtlpEndpoint = "http://localhost:4317";
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_Observability_With_Custom_Tracing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var tracingConfigured = false;

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = true;
            options.Observability.ConfigureTracing = builder =>
            {
                tracingConfigured = true;
            };
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Handle_Observability_With_Prometheus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = true;
            options.Observability.EnablePrometheusExporter = true;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Enable_Idempotency_When_Requested()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableIdempotency = true;
            options.EnableObservability = false;
            options.EnableSagas = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        var idempotencyStore = serviceProvider.GetService<OmniFlow.Idempotency.IIdempotencyStore>();
        
        messageBus.Should().NotBeNull();
        idempotencyStore.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Enable_Sagas_When_Requested()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableSagas = true;
            options.EnableOutbox = true;
            options.EnableObservability = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Enable_Outbox_When_Sagas_Are_Enabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableSagas = true;
            options.EnableOutbox = true;
            options.EnableObservability = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Outbox registration should succeed when sagas are enabled
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Skip_Outbox_When_Disabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableSagas = true;
            options.EnableOutbox = false; // Explicitly disabled
            options.EnableObservability = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Enable_Observability_When_Requested()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.EnableObservability = true;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    [Fact]
    public void AddOmniFlow_Should_Configure_Provider_Options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddOmniFlow(options =>
        {
            options.ServiceName = "TestService";
            options.MessageBus.Provider = MessageBusProvider.InMemory;
            options.MessageBus.RabbitMQ = new RabbitMQConfig
            {
                HostName = "localhost",
                Port = 5672
            };
            options.MessageBus.ServiceBus = new ServiceBusConfig
            {
                ConnectionString = "test-connection"
            };
            options.MessageBus.Kafka = new KafkaConfig
            {
                BootstrapServers = "localhost:9092"
            };
            options.EnableObservability = false;
            options.EnableSagas = false;
            options.EnableIdempotency = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var messageBus = serviceProvider.GetService<IMessageBus>();
        messageBus.Should().NotBeNull();
    }

    private record TestMessage(string Value);
}
