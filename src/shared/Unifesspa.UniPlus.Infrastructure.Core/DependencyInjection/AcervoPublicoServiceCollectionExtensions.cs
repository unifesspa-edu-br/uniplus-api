namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Storage;

/// <summary>
/// Registra <see cref="AcervoPublicoOptions"/> a partir da seção <c>AcervoPublico</c>, com a mesma
/// régua do CORS e do storage: leniente em Development, falha na partida fora dele.
/// </summary>
public static class AcervoPublicoServiceCollectionExtensions
{
    /// <summary>
    /// Liga e valida <see cref="AcervoPublicoOptions"/>. Só o deployable que publica documento no
    /// acervo a chama; o bucket e a leitura passam pelo <see cref="IStorageService"/> já registrado
    /// por <see cref="StorageServiceCollectionExtensions.AddUniPlusStorage"/>.
    /// </summary>
    public static IServiceCollection AddUniPlusAcervoPublico(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        OptionsBuilder<AcervoPublicoOptions> options = services.AddOptions<AcervoPublicoOptions>()
            .Bind(configuration.GetSection(AcervoPublicoOptions.SectionName));
        ExigirAcervoConfiguradoForaDoDesenvolvimento(options, environment).ValidateOnStart();

        return services;
    }

    private static OptionsBuilder<AcervoPublicoOptions> ExigirAcervoConfiguradoForaDoDesenvolvimento(
        OptionsBuilder<AcervoPublicoOptions> options, IHostEnvironment environment) =>
        options
            .Validate(
                valores => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(valores.Bucket),
                "AcervoPublico:Bucket must be configured outside Development.")
            .Validate(
                valores => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(valores.EnderecoBase),
                "AcervoPublico:EnderecoBase must be configured outside Development — it is the edge address every published document link is built on.")
            .Validate(
                valores => string.IsNullOrWhiteSpace(valores.EnderecoBase)
                    || EhEnderecoBaseValido(valores.EnderecoBase, exigeHttps: !environment.IsDevelopment()),
                "AcervoPublico:EnderecoBase must be an absolute https URI without query or fragment (http is accepted only in Development).");

    private static bool EhEnderecoBaseValido(string enderecoBase, bool exigeHttps) =>
        Uri.TryCreate(enderecoBase, UriKind.Absolute, out Uri? endereco)
        && (endereco.Scheme == Uri.UriSchemeHttps || (!exigeHttps && endereco.Scheme == Uri.UriSchemeHttp))
        && string.IsNullOrEmpty(endereco.Query)
        && string.IsNullOrEmpty(endereco.Fragment);
}
