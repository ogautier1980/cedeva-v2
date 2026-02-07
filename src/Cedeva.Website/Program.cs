using Autofac;
using Autofac.Extensions.DependencyInjection;
using Cedeva.Core.Entities;
using Cedeva.Core.Interfaces;
using Cedeva.Infrastructure.Data;
using Cedeva.Infrastructure.Identity;
using Cedeva.Infrastructure.Services;
using Cedeva.Infrastructure.Services.Activities;
using Cedeva.Infrastructure.Services.Email;
using Cedeva.Infrastructure.Services.Excel;
using Cedeva.Infrastructure.Services.Financial;
using Cedeva.Infrastructure.Services.Pdf;
using Cedeva.Infrastructure.Services.Storage;
using Cedeva.Website.Infrastructure;
using Cedeva.Website.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Cedeva application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Register storage service based on environment
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddScoped<IStorageService, LocalFileStorageService>();
    }
    else
    {
        builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
    }

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
        containerBuilder.RegisterType<ClosedXmlExportService>().As<IExcelExportService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<QuestPdfExportService>().As<IPdfExportService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BelgianMunicipalityService>().As<IBelgianMunicipalityService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<StructuredCommunicationService>().As<IStructuredCommunicationService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<CodaParserService>().As<ICodaParserService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<BankReconciliationService>().As<IBankReconciliationService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ExcursionService>().As<IExcursionService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ExcursionViewModelBuilderService>().As<IExcursionViewModelBuilderService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<FinancialCalculationService>().As<IFinancialCalculationService>().InstancePerLifetimeScope();
        containerBuilder.RegisterType<ActivitySelectionService>().As<IActivitySelectionService>().InstancePerLifetimeScope();
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
    .AddClaimsPrincipalFactory<CedevaUserClaimsPrincipalFactory>();

    // Configure cookie
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

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

    // Add localization
    builder.Services.AddLocalization();
    builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        var supportedCultures = new[] { "fr-BE", "nl-BE", "en-US" };
        options.SetDefaultCulture("fr-BE")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        // Use cookie for culture preference
        options.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
    });

    // Add DbSeeder
    builder.Services.AddScoped<DbSeeder>();
    builder.Services.AddScoped<TestDataSeeder>();

    var app = builder.Build();

    // Seed database
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();

        // Seed test data
        var testDataSeeder = scope.ServiceProvider.GetRequiredService<TestDataSeeder>();
        await testDataSeeder.SeedTestDataAsync();
    }

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
