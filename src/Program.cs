using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Minio;
using Planara.Common.Auth.Jwt;
using Planara.Common.Configuration;
using Planara.Common.Database;
using Planara.Common.Host;
using Planara.Common.Kafka;
using Planara.Common.Validators;
using Planara.Files.Data;
using Planara.Files.Interfaces;
using Planara.Files.Options;
using Planara.Files.Services;
using Planara.Files.Workers;
using Planara.Kafka.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddSettingsJson();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Planara API",
            Version = "v1"
        });

        options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header"
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearer", document)] = []
        });
    });
}

builder.Services
    .AddValidators(Assembly.GetExecutingAssembly())
    .AddHttpContextAccessor()
    .AddJwtAuth(builder.Configuration)
    // .AddCors()
    .AddLogging()
    .AddControllers();

builder.Services.AddDataContext<DataContext>(
    builder.Configuration.GetValue<string>("DbConnections:Postgres:ConnectionString")!,
    builder.Configuration.GetValue<int>("DbConnections:Postgres:MaxRetry"),
    builder.Configuration.GetValue<int>("DbConnections:Postgres:MaxDelaySec")
);

builder.Services.AddKafkaConsumer<UserDeletedMessage>(builder.Configuration);
builder.Services.AddHostedService<UserDeletedKafkaConsumerWorker>();

builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IObjectStorage, ObjectStorage>();
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;

    var client = new MinioClient()
        .WithEndpoint(options.Endpoint)
        .WithCredentials(options.AccessKey, options.SecretKey);

    if (options.UseSsl)
        client = client.WithSSL();

    return client.Build();
});

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.PrepareAndRun<DataContext>(args);