namespace Unifesspa.UniPlus.Portal.UnitTests.Origens;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

using Unifesspa.UniPlus.Portal.Infrastructure.Origens;

/// <summary>
/// Monta as origens pelo registro real — política de resiliência, cabeçalhos e manipulador —,
/// trocando apenas a rede.
/// </summary>
internal static class OrigensMontagem
{
    public const string EnderecoSelecao = "http://selecao.origem.test";
    public const string EnderecoPublicacoes = "http://publicacoes.origem.test";

    public static ServiceProvider Montar(RedeSimulada rede, IDictionary<string, string?>? ajustes = null)
    {
        Dictionary<string, string?> valores = new()
        {
            [$"{OrigensOptions.SectionName}:{nameof(OrigensOptions.SelecaoBaseUrl)}"] = EnderecoSelecao,
            [$"{OrigensOptions.SectionName}:{nameof(OrigensOptions.PublicacoesBaseUrl)}"] = EnderecoPublicacoes,
            [$"{OrigensOptions.SectionName}:{nameof(OrigensOptions.MaximoDeRetentativas)}"] = "1",

            // Sem espera entre tentativas: o teste prova quantas vezes se tenta, não quanto se espera.
            [$"{OrigensOptions.SectionName}:{nameof(OrigensOptions.EsperaBaseEntreTentativasEmMilissegundos)}"] = "0",
        };

        foreach (KeyValuePair<string, string?> ajuste in ajustes ?? new Dictionary<string, string?>())
        {
            valores[$"{OrigensOptions.SectionName}:{ajuste.Key}"] = ajuste.Value;
        }

        IConfiguration configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddPortalOrigens(configuracao);

        servicos.ConfigureAll<HttpClientFactoryOptions>(opcoes =>
            opcoes.HttpMessageHandlerBuilderActions.Add(construtor => construtor.PrimaryHandler = rede));

        return servicos.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
