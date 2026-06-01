using Amazon.Translate;
using Amazon.Translate.Model;
using NetCoreBackend.NArchitecture.Core.Translation.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate;

public class AmazonTranslateLocalizationManager : ITranslationService, IDisposable
{
    private readonly AmazonTranslateClient _client;
    private bool _disposed;

    public AmazonTranslateLocalizationManager(AmazonTranslateConfiguration configuration)
    {
        _client = new AmazonTranslateClient(configuration.AccessKey, configuration.SecretKey, configuration.RegionEndpoint);
    }

    public async Task<string> TranslateAsync(string text, string to, string from = "en")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TranslateTextRequest request =
            new()
            {
                SourceLanguageCode = from,
                TargetLanguageCode = to,
                Text = text,
            };

        TranslateTextResponse response = await _client.TranslateTextAsync(request);
        return response.TranslatedText;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
