namespace Api.Infrastructure;

public static class SentryWebHostExtensions
{
    public static IWebHostBuilder AddSentry(this IWebHostBuilder builder)
    {
        builder.UseSentry((context, options) =>
        {
            options.Dsn = context.Configuration["Sentry:Dsn"] ?? string.Empty;
            options.Environment = context.HostingEnvironment.EnvironmentName;
        });
        return builder;
    }
}
