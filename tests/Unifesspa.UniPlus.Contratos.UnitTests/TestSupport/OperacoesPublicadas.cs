namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

/// <summary>Os documentos OpenAPI que cada executável do produto gera em execução, lidos uma só vez por processo de teste.</summary>
internal static class OperacoesPublicadas
{
    private const string PrefixoDoArquivo = "openapi.";

    private static readonly Lazy<Task<IReadOnlyList<DocumentoPublicado>>> Documentos =
        new(LerDocumentosAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task<IReadOnlyList<DocumentoPublicado>> LerAsync() => Documentos.Value;

    /// <summary>Os nomes dos documentos que o repositório versiona em <c>contracts/openapi.*.json</c>.</summary>
    public static IReadOnlyList<string> NomesDosContratos() =>
        [.. OpenApiBaselines.Paths().Select(static caminho => Path.GetFileNameWithoutExtension(caminho)[PrefixoDoArquivo.Length..])];

    private static async Task<IReadOnlyList<DocumentoPublicado>> LerDocumentosAsync()
    {
        List<DocumentoPublicado> documentos = [];
        foreach (string executavel in HostsDoProdutoIniciados.Executaveis)
        {
            documentos.AddRange(await LerDoExecutavelAsync(executavel));
        }

        return documentos;
    }

    private static async Task<IReadOnlyList<DocumentoPublicado>> LerDoExecutavelAsync(string executavel)
    {
        HostIniciado host = HostsDoProdutoIniciados.Obter(executavel);
        List<DocumentoPublicado> documentos = [];
        foreach (string nome in NomesDosDocumentosRegistrados(host))
        {
            IOpenApiDocumentProvider provedor = host.ProvedorDeServicos.GetRequiredKeyedService<IOpenApiDocumentProvider>(nome);
            documentos.Add(Publicar(executavel, nome, await provedor.GetOpenApiDocumentAsync()));
        }

        return documentos;
    }

    /// <summary>Os documentos que o executável registrou, com ou sem arquivo em <c>contracts/</c>.</summary>
    private static IEnumerable<string> NomesDosDocumentosRegistrados(HostIniciado host) =>
        host.RegistrosDeServico
            .Where(static registro => registro.IsKeyedService && registro.ServiceType == typeof(IOpenApiDocumentProvider))
            .Select(static registro => (string)registro.ServiceKey!)
            .Distinct(StringComparer.Ordinal);

    private static DocumentoPublicado Publicar(string executavel, string nome, OpenApiDocument documento) =>
        new(executavel, nome,
        [.. documento.Paths.SelectMany(caminho => (caminho.Value.Operations ?? []).Select(operacao =>
            new OperacaoPublicada(executavel, nome, operacao.Key.Method, caminho.Key, operacao.Value.OperationId,
                ParametrosDeConsultaPublicados.De(caminho.Value, operacao.Value))))]);
}
