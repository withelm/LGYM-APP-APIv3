using LgymApi.Infrastructure.Data;
using LgymApi.Domain.ValueObjects;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LgymApi.Payments.Contracts;
using LgymApi.Payments.Providers.Stripe;
using Microsoft.Extensions.Logging;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Configure the web application host for integration testing with in-memory database and test email sender.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly bool _usePaymentsRealFlow;

    public CustomWebApplicationFactory() : this(usePaymentsRealFlow: false)
    {
    }

    private CustomWebApplicationFactory(bool usePaymentsRealFlow)
    {
        _usePaymentsRealFlow = usePaymentsRealFlow;
    }

    internal static CustomWebApplicationFactory CreatePaymentsRealFlowFactory() => new(usePaymentsRealFlow: true);

    /// <summary>
    /// Gets a unique database name for this test instance.
    /// </summary>
    public string DatabaseName { get; } = $"lgymtests_{Id<CustomWebApplicationFactory>.New():N}";
    /// <summary>
    /// Gets the test email sender instance for capturing outbound emails during tests.
    /// </summary>
    public TestEmailSender EmailSender { get; } = new();

    public TestPushProviderSender PushSender { get; } = new();

    /// <summary>
    /// The JWT signing key used for generating test tokens during integration tests.
    /// </summary>
    public const string TestJwtSigningKey = "IntegrationTestSigningKey_MustBeAtLeast32Characters!";

    /// <summary>
    /// Configure the test web host with in-memory database, JWT settings, email configuration, and CORS policies.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
        builder.ConfigureServices(services =>
        {
            PaymentCheckoutTestStore.Reset();
            RemoveAppDbContextRegistrations(services);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(DatabaseName, _databaseRoot);
                options.EnableSensitiveDataLogging();
            });

            IntegrationHostServiceOverrides.ReplaceExternalEffects(services, EmailSender, PushSender);
            if (_usePaymentsRealFlow)
            {
                services.RemoveAll<StripePaymentOptions>();
                services.RemoveAll<IStripeCheckoutGateway>();
                services.RemoveAll<IStripeWebhookVerifier>();
                services.AddSingleton(new StripePaymentOptions
                {
                    Enabled = true,
                    PriceId = "price_test_scripted",
                    SuccessUrl = "https://app.test.local/payments/success",
                    CancelUrl = "https://app.test.local/payments/cancel",
                    ApiKey = "sk_test_scripted",
                    WebhookSigningSecret = "whsec_test_scripted"
                });
                services.AddSingleton<IStripeCheckoutGateway, ScriptedStripeCheckoutGateway>();
                services.AddSingleton<IStripeWebhookVerifier, ScriptedStripeWebhookVerifier>();
            }
            else
            {
                services.RemoveAll<IPaymentsApi>();
                services.AddSingleton<IPaymentsApi, TestPaymentsApi>();
            }

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            TestDataFactory.SeedDefaultRolesAsync(db).GetAwaiter().GetResult();
            db.SaveChanges();
        });

        builder.UseSetting("Jwt:SigningKey", TestJwtSigningKey);
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost");
        builder.UseSetting("Email:Enabled", "true");
        builder.UseSetting("Email:FromAddress", "no-reply@test.local");
        builder.UseSetting("Email:SmtpHost", "localhost");
        builder.UseSetting("Email:SmtpPort", "1025");
        builder.UseSetting("Email:InvitationBaseUrl", "https://app.test.local/invitations");
        builder.UseSetting("Email:TemplateRootPath", Path.Combine(AppContext.BaseDirectory, "EmailTemplates"));
        builder.UseSetting("Email:DefaultCulture", "en-US");
    }

    internal static void RemoveAppDbContextRegistrations(IServiceCollection services)
        => IntegrationHostServiceOverrides.RemoveAppDbContextRegistrations(services);

    private sealed class ScriptedStripeCheckoutGateway : IStripeCheckoutGateway
    {
        private StripeCheckoutSession? _session;

        public Task<StripeCheckoutSession> CreateAsync(StripeCheckoutCreateRequest request, CancellationToken cancellationToken)
        {
            _session = new StripeCheckoutSession(
                "cs_test_scripted_payment",
                "https://checkout.test.local/scripted-session",
                false,
                "paid",
                request.Metadata,
                request.PriceId,
                request.Quantity);
            return Task.FromResult(_session);
        }

        public Task<StripeCheckoutSession> RetrieveAsync(string providerSessionReference, CancellationToken cancellationToken)
        {
            if (_session is null || !string.Equals(_session.ProviderSessionReference, providerSessionReference, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The scripted Stripe Checkout Session was not created.");
            }

            return Task.FromResult(_session);
        }
    }

    private sealed class ScriptedStripeWebhookVerifier : IStripeWebhookVerifier
    {
        public bool TryVerify(ReadOnlyMemory<byte> rawBody, string signature, out StripeWebhookEvent stripeEvent)
        {
            if (string.Equals(signature, "scripted-valid-signature", StringComparison.Ordinal) &&
                rawBody.Span.SequenceEqual("scripted-payment-event"u8))
            {
                stripeEvent = new StripeWebhookEvent(
                    "evt_scripted_payment",
                    "checkout.session.completed",
                    false,
                    "cs_test_scripted_payment");
                return true;
            }

            stripeEvent = default!;
            return false;
        }
    }
}
