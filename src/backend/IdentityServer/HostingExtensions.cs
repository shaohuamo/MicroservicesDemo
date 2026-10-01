using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using IdentityServer.Data;
using IdentityServer.Extensions;
using IdentityServer.Health;
using IdentityServer.Localization;
using IdentityServer.Models;
using IdentityServer.Options;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Resend;
using Serilog;
using Serilog.Filters;

namespace IdentityServer;

internal static class HostingExtensions
{
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        // Write most logs to the console but diagnostic data to a file.
        // See https://docs.duendesoftware.com/identityserver/diagnostics/data
        builder.Services.AddSerilog(lc =>
        {
            lc.WriteTo.Logger(consoleLogger =>
            {
                consoleLogger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    formatProvider: CultureInfo.InvariantCulture);
                if (builder.Environment.IsDevelopment())
                {
                    consoleLogger.Filter.ByExcluding(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }
            });
            if (builder.Environment.IsDevelopment())
            {
                lc.WriteTo.Logger(fileLogger =>
                {
                    fileLogger
                        .WriteTo.File("./diagnostics/diagnostic.log", rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: 1024 * 1024 * 10, // 10 MB
                            rollOnFileSizeLimit: true,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                            formatProvider: CultureInfo.InvariantCulture)
                        .Filter
                        .ByIncludingOnly(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }).Enrich.FromLogContext().ReadFrom.Configuration(builder.Configuration);
            }
        });
        return builder;
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.AddObservability();

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddRazorPages()
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddDataAnnotationsLocalization(options =>
            {
                options.DataAnnotationLocalizerProvider = (_, factory) =>
                    factory.Create(typeof(SharedResource));
            });

        var supportedCultures = CultureCookieHelper.SupportedCultures
            .Select(culture => new CultureInfo(culture))
            .ToArray();
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider
                {
                    CookieName = CultureCookieHelper.CookieName,
                },
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.SectionName));
        builder.Services.Configure<SeedUserOptions>(builder.Configuration.GetSection(SeedUserOptions.SectionName));
        builder.Services.Configure<SigningCredentialOptions>(
            builder.Configuration.GetSection(SigningCredentialOptions.SectionName));
        builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
        builder.Services.Configure<ResendEmailOptions>(builder.Configuration.GetSection(ResendEmailOptions.SectionName));
        builder.Services.Configure<EmailVerificationRateLimitOptions>(
            builder.Configuration.GetSection(EmailVerificationRateLimitOptions.SectionName));
        builder.Services.PostConfigure<ResendEmailOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            {
                options.PublicBaseUrl = builder.Configuration["IdentityServer:IssuerUri"];
            }

            if (string.IsNullOrWhiteSpace(options.ConfirmationSubject))
            {
                options.ConfirmationSubject = "Confirm your MicroservicesDemo account";
            }
        });

        var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            builder.Services
                .AddDataProtection()
                .SetApplicationName("MicroservicesDemo.IdentityServer")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }

        builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            var connectionString = BuildPostgresConnectionString(builder.Configuration, postgresOptions);

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: postgresOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            });

            if (builder.Environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.SignIn.RequireConfirmedEmail = true;
            options.User.RequireUniqueEmail = true;
        });

        builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        builder.Services.AddResend(options =>
        {
            options.ApiToken = builder.Configuration["ResendEmail:ApiToken"] ?? string.Empty;
        });
        builder.Services.AddScoped<EmailConfirmationLinkFactory>();
        builder.Services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
        builder.Services.AddSingleton<IEmailVerificationRateLimiter, RedisEmailVerificationRateLimiter>();
        builder.Services.AddTransient<IIdentityEmailSender, ResendIdentityEmailSender>();

        var identityServerMigrationsAssembly = typeof(Program).Assembly.GetName().Name;
        var operationalStorePostgresOptions =
            builder.Configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new PostgresOptions();
        var operationalStoreConnectionString = BuildPostgresConnectionString(
            builder.Configuration,
            operationalStorePostgresOptions);

        var identityServerBuilder = builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
                options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;
                options.KeyManagement.Enabled = false;

                var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
                if (!string.IsNullOrWhiteSpace(issuerUri))
                {
                    options.IssuerUri = issuerUri;
                }
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = dbContextBuilder =>
                    dbContextBuilder.UseNpgsql(operationalStoreConnectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(identityServerMigrationsAssembly);
                    });

                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600;
            });

        ConfigureSigningCredential(identityServerBuilder, builder)
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiResources(Config.ApiResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryClients(Config.GetClients(builder.Configuration))
            .AddAspNetIdentity<ApplicationUser>()
            .AddProfileService<ApplicationProfileService>();

        builder.Services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<IdentityDatabaseHealthCheck>("identity_database", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        builder.Services.AddSingleton<IIdentityDatabaseHealthProbe, IdentityDatabaseHealthProbe>();

        return builder.Build();
    }

    private static IIdentityServerBuilder ConfigureSigningCredential(
        IIdentityServerBuilder identityServerBuilder,
        WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            var keysPath = builder.Configuration["DataProtection:KeysPath"];
            var signingKeyFile = string.IsNullOrWhiteSpace(keysPath)
                ? "tempkey.jwk"
                : Path.Combine(keysPath, "tempkey.jwk");
            return identityServerBuilder.AddDeveloperSigningCredential(
                persistKey: true, filename: signingKeyFile);
        }

        var signingCredentialOptions = builder.Configuration
            .GetSection(SigningCredentialOptions.SectionName)
            .Get<SigningCredentialOptions>() ?? new SigningCredentialOptions();

        if (string.IsNullOrWhiteSpace(signingCredentialOptions.CertificatePath))
        {
            throw new InvalidOperationException(
                "IdentityServer signing certificate is required outside Development. " +
                "Set IdentityServer:SigningCredential:CertificatePath.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            signingCredentialOptions.CertificatePath,
            signingCredentialOptions.CertificatePassword);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "IdentityServer signing certificate must include a private key.");
        }

        return identityServerBuilder.AddSigningCredential(certificate);
    }

    private static string BuildPostgresConnectionString(IConfiguration configuration, PostgresOptions postgresOptions)
    {
        var connectionStringTemplate = configuration.GetConnectionString("PostgresConnection")!;
        return connectionStringTemplate
            .Replace("$POSTGRES_HOST", postgresOptions.Host)
            .Replace("$POSTGRES_PASSWORD", postgresOptions.Password)
            .Replace("$POSTGRES_DATABASE", postgresOptions.Database)
            .Replace("$POSTGRES_PORT", postgresOptions.Port)
            .Replace("$POSTGRES_USER", postgresOptions.User);
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRequestLocalization();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });

        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}
