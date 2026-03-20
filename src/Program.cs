using System.Reflection;
using Planara.Common.Auth.Jwt;
using Planara.Common.Configuration;
using Planara.Common.Database;
using Planara.Common.Host;
using Planara.Common.Validators;
using Planara.Files.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddSettingsJson();
builder.Services
    .AddValidators(Assembly.GetExecutingAssembly())
    .AddHttpContextAccessor()
    .AddJwtAuth(builder.Configuration)
    // .AddCors()
    .AddLogging();

builder.Services.AddDataContext<DataContext>(
    builder.Configuration.GetValue<string>("DbConnections:Postgres:ConnectionString")!,
    builder.Configuration.GetValue<int>("DbConnections:Postgres:MaxRetry"),
    builder.Configuration.GetValue<int>("DbConnections:Postgres:MaxDelaySec")
);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.PrepareAndRun<DataContext>(args);