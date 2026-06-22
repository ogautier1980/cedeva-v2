using Autofac;
using Autofac.Extensions.DependencyInjection;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Configuration;
using Cedeva.Infrastructure.Data;
using Cedeva.Infrastructure.Identity;
using Cedeva.Infrastructure.Services;
using Cedeva.Infrastructure.Services.Activities;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Infrastructure.Services.Excel;
using Cedeva.Infrastructure.Services.Import;
using Cedeva.Infrastructure.Services.Financial;
using Cedeva.Infrastructure.Services.Payments;
using Cedeva.Infrastructure.Services.Pdf;
using Cedeva.Infrastructure.Services.Storage;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Cedeva.Website.Validation;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Cedeva application");

    var builder = WebApplication.CreateBuilder(args);

    // Don't advertise the server implementation (removes the "Server: Kestrel" header).
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    // Configure Serilog: read sinks from config, enrich with runtime context, and add a
    // Seq sink only when Seq:ServerUrl is configured (so it stays inert without a Seq server).
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId();

        var seqUrl = context.Configuration["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            configuration.WriteTo.Seq(seqUrl, apiKey: context.Configuration["Seq:ApiKey"]);
        }
    });

    // Application Insights (Azure-native observability: requests, dependencies, failures, live
    // metrics) — enabled only when a connection string is configured (ApplicationInsights:
    // ConnectionString or the APPLICATIONINSIGHTS_CONNECTION_STRING env var, e.g. an Azure app
    // setting). Inert in dev/CI where it's unset, mirroring the Seq sink above.
    var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
        ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
            options.ConnectionString = appInsightsConnectionString);
    }

    // Strongly-typed configuration (Options pattern)
    builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection(BrevoOptions.SectionName));
    builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection(AzureStorageOptions.SectionName));
    builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));

    // Rate limiting (per client IP) for sensitive anonymous endpoints: login and the public
    // registration flow — mitigates brute-force and bot spam. Client IP is resolved from the
    // forwarded headers configured above.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

        options.AddPolicy("public-registration", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    });

    // Register storage service based on environment
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<IStorageService, LocalFileStorageService>();
    }
    else
    {
        builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
    }

    builder.Services.AddHttpClient("BrevoClient", client =>
    {
        var apiBaseUrl = builder.Configuration["Brevo:ApiBaseUrl"]
            ?? throw new InvalidOperationException("Brevo:ApiBaseUrl not configured in appsettings.json");
        client.BaseAddress = new Uri(apiBaseUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        var apiKey = builder.Configuration["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Brevo:ApiKey not configured in appsettings.json");
        client.DefaultRequestHeaders.Add("api-key", apiKey);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        ConnectCallback = async (context, cancellationToken) =>
        {
            var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
            var ipAddress = entry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                throw new InvalidOperationException($"No IPv4 address found for {context.DnsEndPoint.Host}");
            }

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            try 
            {
                await socket.ConnectAsync(ipAddress, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    });

    // Note: IEmailService is NOT registered here - it's registered in Autofac below
    // Brevo SDK configuration is handled internally by BrevoEmailService constructor

    // Configure Autofac
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        // Register services
        containerBuilder.RegisterType<CurrentUserService>().As<ICurrentUserService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BrevoEmailService>().As<IEmailService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<EmailRecipientService>().As<IEmailRecipientService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<EmailVariableReplacementService>().As<IEmailVariableReplacementService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<EmailTemplateService>().As<IEmailTemplateService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<EmailFacadeService>().As<IEmailFacadeService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ClosedXmlExportService>().As<IExcelExportService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<QuestPdfExportService>().As<IPdfExportService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ExportFacadeService>().As<IExportFacadeService>().InstancePerLifetimeScope();
        containerBuilder.RegisterGeneric(typeof(CedevaControllerContext<>)).As(typeof(ICedevaControllerContext<>)).InstancePerLifetimeScope();
        containerBuilder.RegisterType<StorageContext>().As<IStorageContext>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BelgianMunicipalityService>().As<IBelgianMunicipalityService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<StructuredCommunicationService>().As<IStructuredCommunicationService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<StripePaymentGateway>().As<IPaymentGateway>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BookingPaymentService>().As<IBookingPaymentService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ExcursionService>().As<IExcursionService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ContactDirectoryService>().As<IContactDirectoryService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ActivityEmailService>().As<IActivityEmailService>().InstancePerLifetimeScope();
        // CSV entity importers (resolved as a set by the generic import controller).
        containerBuilder.RegisterType<ParentCsvImporter>().As<ICsvEntityImporter>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<TeamMemberCsvImporter>().As<ICsvEntityImporter>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ContactCsvImporter>().As<ICsvEntityImporter>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ActivityCsvImporter>().As<ICsvEntityImporter>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<OrganisationCsvImporter>().As<ICsvEntityImporter>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ExcursionViewModelBuilderService>().As<IExcursionViewModelBuilderService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<FinancialCalculationService>().As<IFinancialCalculationService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<SessionStateService>().As<ISessionStateService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BookingQuestionService>().As<IBookingQuestionService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<UserDisplayService>().As<IUserDisplayService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();
        containerBuilder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>)).InstancePerLifetimeScope();
    });

    // Add DbContext
    builder.Services.AddDbContext<CedevaDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null)));

    // Health checks — /health verifies the app can reach the database (used by the deploy gate).
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<CedevaDbContext>();

    // Add Identity
    builder.Services.AddIdentity<CedevaUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<CedevaDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddClaimsPrincipalFactory<CedevaUserClaimsPrincipalFactory>();

    // Configure cookie
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // Harden the auth cookie: HTTPS-only (prod) + HttpOnly + SameSite=Lax (Lax keeps normal
        // navigation/login working; antiforgery tokens cover state-changing POST CSRF).
        // SameAsRequest in Development so local http://localhost login still works.
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // Add FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddFluentValidationClientsideAdapters();

    // Add MVC with feature folders
    builder.Services.AddControllersWithViews()
        .AddViewLocalization()
        .AddDataAnnotationsLocalization(options =>
        {
            options.DataAnnotationLocalizerProvider = (type, factory) =>
                factory.Create(typeof(SharedResources));
        })
        .AddRazorOptions(options =>
        {
            // Feature folder view locations
            options.ViewLocationFormats.Clear();
            options.ViewLocationFormats.Add("/Features/{1}/{0}.cshtml");
            options.ViewLocationFormats.Add("/Features/Shared/{0}.cshtml");
            options.ViewLocationFormats.Add("/Features/Shared/Layouts/{0}.cshtml");
            options.ViewLocationFormats.Add("/Features/Shared/Components/{0}.cshtml");
        });

    // Add HttpContextAccessor
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<SessionState>();

    // Add Session
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    // Add localization with custom cultures (all use EUR currency)
    builder.Services.AddLocalization();
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        // Create custom Belgian cultures that all use EUR (€) as currency for formatting
        var frBE = new System.Globalization.CultureInfo("fr-BE");
        var nlBE = new System.Globalization.CultureInfo("nl-BE");
        var enBE = new System.Globalization.CultureInfo("en-BE");

        // Ensure all cultures use EUR currency symbol
        frBE.NumberFormat.CurrencySymbol = "€";
        nlBE.NumberFormat.CurrencySymbol = "€";
        enBE.NumberFormat.CurrencySymbol = "€";

        // SupportedCultures: for formatting numbers/dates/currency (uses Belgian formats with €)
        options.SupportedCultures = new[] { frBE, nlBE, enBE };

        // SupportedUICultures: for resource file selection (uses neutral cultures fr/nl/en)
        // This matches the resource file names (SharedResources.fr.resx, etc.)
        options.SupportedUICultures = new[] {
            new System.Globalization.CultureInfo("fr"),
            new System.Globalization.CultureInfo("nl"),
            new System.Globalization.CultureInfo("en")
        };

        // Default: French with Belgian formatting
        options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(
            culture: "fr-BE",  // for formatting
            uiCulture: "fr"    // for resource files
        );

        // Use cookie for culture preference
        options.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
    });

    // Honour X-Forwarded-* from Azure App Service's TLS-terminating reverse proxy so the
    // app sees the real scheme (https) — required for HSTS / HttpsRedirection to work.
    // The only ingress is the platform proxy, so the proxy/network allow-lists are cleared.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Add DbSeeder
    builder.Services.AddScoped<DbSeeder>();
    builder.Services.AddScoped<TestDataSeeder>();

    var app = builder.Build();

    // Seed the database AFTER the app starts listening, in the background.
    // This keeps slow or failing seeding off the HTTP startup path: the platform
    // warmup/health probe succeeds immediately (reliable, fast deployments) and a
    // transient DB hiccup logs an error instead of crashing the worker. Schema
    // migrations are also applied by the CI/CD pipeline before deploy.
    // Demo/test data is only seeded in Development.
    // Background seeding can be disabled (e.g. in integration tests) via config.
    if (app.Configuration.GetValue("RunStartupSeeding", true))
    {
        app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
        {
            try
            {
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();

                // Rich demo/test data: on by default in Development, and toggleable elsewhere via
                // the SeedDemoData flag (e.g. set SeedDemoData=true on the Azure demo site, false in
                // real production). Absent config falls back to the environment.
                if (app.Configuration.GetValue("SeedDemoData", app.Environment.IsDevelopment()))
                {
                    await scope.ServiceProvider.GetRequiredService<TestDataSeeder>().SeedTestDataAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database seeding failed at startup; the application will keep running");
            }
        }));
    }

    // Must run first so downstream middleware sees the forwarded scheme/host.
    app.UseForwardedHeaders();

    // Baseline security response headers (X-Content-Type-Options, X-Frame-Options, etc.).
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Configure the HTTP request pipeline
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRequestLocalization(); // Must be before UseRouting for culture detection
    app.UseRouting();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapHealthChecks("/health");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;
