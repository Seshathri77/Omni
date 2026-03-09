using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Let Aspire manage credentials via Parameters — these stay in sync with the connection string
var rabbitUser = builder.AddParameter("rabbit-user", secret: false);
var rabbitPassword = builder.AddParameter("rabbit-password", secret: true);

// Infrastructure
var rabbitmq = builder.AddRabbitMQ("rabbitmq", userName: rabbitUser, password: rabbitPassword)
    .WithManagementPlugin()
    .WithDataVolume();

var jaeger = builder.AddContainer("jaeger", "jaegertracing/all-in-one", "latest")
    .WithHttpEndpoint(port: 16686, targetPort: 16686, name: "ui")
    .WithEndpoint(port: 4317, targetPort: 4317, name: "otlp-grpc", scheme: "http")
    .WithEndpoint(port: 4318, targetPort: 4318, name: "otlp-http")
    .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true");

var prometheus = builder.AddContainer("prometheus", "prom/prometheus", "latest")
    .WithBindMount("../observability/prometheus.yml", "/etc/prometheus/prometheus.yml")
    .WithHttpEndpoint(port: 9090, targetPort: 9090, name: "ui");

var grafana = builder.AddContainer("grafana", "grafana/grafana", "latest")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "ui")
    .WithEnvironment("GF_SECURITY_ADMIN_PASSWORD", "admin")
    .WithEnvironment("GF_USERS_ALLOW_SIGN_UP", "false")
    .WithBindMount("../observability/grafana/dashboards", "/etc/grafana/provisioning/dashboards")
    .WithBindMount("../observability/grafana/datasources", "/etc/grafana/provisioning/datasources");

// Services
var orderService = builder.AddProject("orderservice", "../ECommerce.OrderService/ECommerce.OrderService.csproj")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("OmniFlow__MessageBus__Provider", "RabbitMQ");

var paymentService = builder.AddProject("paymentservice", "../ECommerce.PaymentService/ECommerce.PaymentService.csproj")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithEnvironment("OmniFlow__MessageBus__Provider", "RabbitMQ");

builder.Build().Run();