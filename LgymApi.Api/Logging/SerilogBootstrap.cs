using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace LgymApi.Api.Logging;

public static class SerilogBootstrap
{
    public static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Application", "lgym-api")
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.With<SensitiveDataEnricher>()
            .WriteTo.Async(a => a.Elasticsearch(
                new[] { new Uri(builder.Configuration["Elasticsearch:Url"] ?? "http://host.docker.internal:9200") },
                opts =>
                {
                    opts.BootstrapMethod = BootstrapMethod.Silent;
                    opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("logs", "lgym", "app");
                }))
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog();
        builder.Services.AddSerilog();
    }
}
