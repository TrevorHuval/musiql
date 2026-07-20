namespace MusiQL.Api.Spotify;

public static class SpotifyServiceCollectionExtensions
{
    public static IServiceCollection AddSpotifyIntegration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpotifyOptions>(configuration.GetSection(SpotifyOptions.Section));

        services.AddSingleton<ITokenProtector, AesGcmTokenProtector>();
        services.AddSingleton(new RequestThrottle(TimeSpan.FromSeconds(1.1)));

        services.AddScoped<SpotifyTokenClient>();
        services.AddScoped<SpotifyAuthService>();
        services.AddScoped<ISpotifyClientFactory, SpotifyClientFactory>();
        services.AddScoped<IMusicBrainzIsrcLookup, MusicBrainzIsrcLookup>();
        services.AddScoped<TrackMatcher>();
        services.AddScoped<CatalogMatcher>();
        services.AddScoped<SpotifyExportService>();
        services.AddScoped<SpotifyLibraryService>();

        services.AddHttpClient(SpotifyClientFactory.HttpClientName,
            client => client.BaseAddress = new Uri("https://api.spotify.com/"));
        services.AddHttpClient(SpotifyTokenClient.HttpClientName,
            client => client.BaseAddress = new Uri("https://accounts.spotify.com/"));
        services.AddHttpClient(MusicBrainzIsrcLookup.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SpotifyOptions>>().Value;
            client.BaseAddress = new Uri("https://musicbrainz.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.MusicBrainzUserAgent);
        });

        return services;
    }
}
