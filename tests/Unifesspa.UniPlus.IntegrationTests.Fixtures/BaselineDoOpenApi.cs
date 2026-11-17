namespace Unifesspa.UniPlus.IntegrationTests.Fixtures;

using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// Confere a especificação OpenAPI servida em execução contra o baseline versionado em <c>contracts/</c>
/// (ADR-0030) e, quando <c>UPDATE_OPENAPI_BASELINE=1</c>, regera o baseline. A variável vale para todos os
/// módulos do processo de teste.
/// </summary>
public static class BaselineDoOpenApi
{
    private const string VariavelDeRegeracao = "UPDATE_OPENAPI_BASELINE";

    private static readonly JsonSerializerOptions OpcoesDeNormalizacao = new() { WriteIndented = true };

    /// <summary>
    /// Resolve o baseline a partir da raiz do repositório e decide regerar pela variável de ambiente.
    /// O chamador é dono do <paramref name="client"/>.
    /// </summary>
    public static Task ConferirAsync(HttpClient client, string rotaDaSpec, string baselineRelativa)
    {
        bool regerar = string.Equals(Environment.GetEnvironmentVariable(VariavelDeRegeracao), "1", StringComparison.Ordinal);

        return ConferirContraOCaminhoAsync(client, rotaDaSpec, CaminhoNaRaiz(baselineRelativa), regerar);
    }

    /// <summary>
    /// Confere (ou regera, quando <paramref name="regerar"/>) o baseline no caminho absoluto informado, sem
    /// ler variável de ambiente. O chamador é dono do <paramref name="client"/>.
    /// </summary>
    public static async Task ConferirContraOCaminhoAsync(
        HttpClient client,
        string rotaDaSpec,
        string caminhoDoBaseline,
        bool regerar)
    {
        ArgumentNullException.ThrowIfNull(client);

        string especificacaoEmExecucao = await ObterNormalizadaAsync(client, rotaDaSpec).ConfigureAwait(false);

        if (regerar)
        {
            await RegerarAsync(caminhoDoBaseline, especificacaoEmExecucao).ConfigureAwait(false);
            return;
        }

        await ConferirContraAsync(caminhoDoBaseline, especificacaoEmExecucao).ConfigureAwait(false);
    }

    private static async Task<string> ObterNormalizadaAsync(HttpClient client, string rotaDaSpec)
    {
        using HttpResponseMessage resposta = await client
            .GetAsync(new Uri(rotaDaSpec, UriKind.Relative))
            .ConfigureAwait(false);
        resposta.EnsureSuccessStatusCode();

        return Normalizar(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
    }

    private static async Task RegerarAsync(string caminhoDoBaseline, string especificacao)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminhoDoBaseline)!);
        await File.WriteAllTextAsync(caminhoDoBaseline, especificacao + "\n").ConfigureAwait(false);
    }

    private static async Task ConferirContraAsync(string caminhoDoBaseline, string especificacaoEmExecucao)
    {
        File.Exists(caminhoDoBaseline).Should().BeTrue(
            $"baseline {RelativoAoRepositorio(caminhoDoBaseline)} precisa estar committed; rode `{VariavelDeRegeracao}=1 dotnet test --filter SpecRuntime` e commit o arquivo gerado.");

        string especificacaoVersionada = Normalizar(await File.ReadAllTextAsync(caminhoDoBaseline).ConfigureAwait(false));

        especificacaoEmExecucao.Should().Be(especificacaoVersionada,
            $"contrato OpenAPI mudou — regerar a baseline com `{VariavelDeRegeracao}=1 dotnet test --filter SpecRuntime` e revisar o diff antes de commit (ADR-0030).");
    }

    private static string Normalizar(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(documento.RootElement, OpcoesDeNormalizacao);
    }

    private static string CaminhoNaRaiz(string caminhoRelativo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoRelativo);
        if (Path.IsPathRooted(caminhoRelativo))
            throw new ArgumentException("O baseline deve ser informado relativo à raiz do repositório.", nameof(caminhoRelativo));

        string raiz = RaizDoRepositorio(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException("UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");

        return Path.Join(raiz, caminhoRelativo);
    }

    private static string RelativoAoRepositorio(string caminho) =>
        RaizDoRepositorio(Path.GetDirectoryName(caminho)) is { } raiz
            ? Path.GetRelativePath(raiz, caminho).Replace('\\', '/')
            : caminho;

    private static string? RaizDoRepositorio(string? aPartirDe)
    {
        string? diretorio = aPartirDe;
        while (diretorio is not null && !File.Exists(Path.Join(diretorio, "UniPlus.slnx")))
            diretorio = Path.GetDirectoryName(diretorio);

        return diretorio;
    }
}
