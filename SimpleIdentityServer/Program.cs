﻿using Hangfire;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
   .AddSingleton(builder.Configuration.GetSection("ApplicationSettings").Get<ApplicationSettings>())
   .AddCommonApplication(builder.Configuration, Assembly.GetExecutingAssembly())
   .AddIdentityApplicationConfiguration(builder.Configuration)
   .AddIdentityInfrastructure(builder.Configuration)
   .AddIdentityWebComponents()
   .AddIdentityModelConfiguration(builder.Configuration)
   .AddTokenAuthentication(builder.Configuration)
   .AddRAGScannerApplication(builder.Configuration)
   .AddRAGScannerInfrastructure(builder.Configuration)
   .AddRAGScannerWebComponents()
   .AddEventSourcing()
   .AddModelBinders()
   .AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Web API", Version = "v1" }))
   .AddHttpClient()
   .AddMcpClient(builder.Configuration)
   .AddMemoryCache()
   .AddDistributedMemoryCache()
   .AddHangfire(config => config.UseSqlServerStorage(builder.Configuration.GetConnectionString("RAGDBConnection")))
   .AddHangfireServer()
   .AddSession(options =>
   {
       options.Cookie.Name = ".Prompting.Session";
       options.Cookie.HttpOnly = true;
       options.Cookie.IsEssential = true;
       options.Cookie.SameSite = SameSiteMode.Lax;
       options.IdleTimeout = TimeSpan.FromMinutes(30);
   })
   .AddHttpContextAccessor()
   .AddHttpClient<IVectorStoreService, VectorStoreService>();

Log.Logger = new LoggerConfiguration()
   .WriteTo.Console()
   .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
   .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var app = builder.Build();

// Ensure cookies are only sent over HTTPS
app.UseCookiePolicy(new CookiePolicyOptions
{
    Secure = CookieSecurePolicy.Always // This ensures cookies are sent only over HTTPS
});

app
   .UseHttpsRedirection()
   .UseSession()
   .UseWebService(app.Environment)
   .Initialize();

app.Run();