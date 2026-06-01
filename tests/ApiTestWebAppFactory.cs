using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planara.Files.Data;
using Testcontainers.PostgreSql;

namespace Planara.Files.Tests;

public class ApiTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string MinioAccessKey = "minioadmin";
    private const string MinioSecretKey = "minioadmin";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("files-test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly IContainer _minio = new ContainerBuilder("minio/minio:latest")
        .WithPortBinding(9000, true)
        .WithEnvironment("MINIO_ROOT_USER", MinioAccessKey)
        .WithEnvironment("MINIO_ROOT_PASSWORD", MinioSecretKey)
        .WithCommand("server", "/data", "--address", ":9000", "--console-address", ":9001")
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort(9000)
                    .ForPath("/minio/health/ready")))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<DataContext>));
            services.RemoveAll(typeof(DataContext));

            services.AddDbContext<DataContext>(opt =>
                opt.UseNpgsql(_postgres.GetConnectionString()));

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            });
        });

        builder.ConfigureAppConfiguration(config =>
        {
            var minioEndpoint = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:Endpoint"] = minioEndpoint
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _minio.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        db.Database.SetCommandTimeout(3000);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await _minio.StopAsync();
    }
}