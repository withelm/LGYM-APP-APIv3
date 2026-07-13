using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace LgymApi.Api.Logging;

    public static class SerilogBootstrap
    {
        public static void ConfigureSerilog(WebApplicationBuilder builder)
        {
            var elasticsearchUrl = builder.Configuration["Elasticsearch:Url"];

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("Application", "lgym-api")
                .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
                .Enrich.With<SensitiveDataEnricher>();

            if (!string.IsNullOrWhiteSpace(elasticsearchUrl))
            {
                loggerConfiguration = loggerConfiguration.WriteTo.Async(a => a.Elasticsearch(
                    new[] { new Uri(elasticsearchUrl) },
                    opts =>
                    {
                        opts.BootstrapMethod = BootstrapMethod.Silent;
                        opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("logs", "lgym", "app");
                    }));
            }

            Log.Logger = loggerConfiguration.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();
        builder.Services.AddSerilog();
    }
}
