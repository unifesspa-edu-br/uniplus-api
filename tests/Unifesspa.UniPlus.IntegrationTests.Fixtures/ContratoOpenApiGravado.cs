namespace Unifesspa.UniPlus.IntegrationTests.Fixtures;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.TestSupport;

/// <summary>Compara o documento em execução com a referência aprovada (ADR-0030).</summary>
public static class ContratoOpenApiGravado
{
    private static readonly JsonSerializerOptions OpcoesDeNormalizacao = new() { WriteIndented = true };

    public static Task ConferirAsync(HttpClient client, string rotaDaSpec, string caminhoRelativoDoContrato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoRelativoDoContrato);
        if (Path.IsPathRooted(caminhoRelativoDoContrato))
            throw new ArgumentException("O caminho deve ser relativo à saída do teste.", nameof(caminhoRelativoDoContrato));
        return ConferirContraOCaminhoAsync(client, rotaDaSpec, Path.Join(AppContext.BaseDirectory, caminhoRelativoDoContrato));
    }

    public static async Task ConferirContraOCaminhoAsync(HttpClient client, string rotaDaSpec, string caminhoDoContrato)
    {
        ArgumentNullException.ThrowIfNull(client);
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(rotaDaSpec, UriKind.Relative)).ConfigureAwait(false);
        resposta.EnsureSuccessStatusCode();
        string obtido = Normalizar(await resposta.Content.ReadAsStringAsync().ConfigureAwait(false));
        (string? aprovado, string motivo) = await LerAprovadoAsync(caminhoDoContrato).ConfigureAwait(false);
        if (!string.Equals(obtido, aprovado, StringComparison.Ordinal))
        {
            string resultado = ResultadoDeConferencia.Gravar(Path.GetFileName(caminhoDoContrato), obtido + "\n");
            obtido.Should().Be(aprovado, $"{motivo}; {ResultadoDeConferencia.Mensagem(resultado, Path.Join("contracts", Path.GetFileName(caminhoDoContrato)))}");
        }
    }

    private static async Task<(string? Conteudo, string Motivo)> LerAprovadoAsync(string caminho)
    {
        if (!File.Exists(caminho))
            return (null, "o contrato OpenAPI está ausente");
        string conteudo = await File.ReadAllTextAsync(caminho).ConfigureAwait(false);
        try
        {
            return (Normalizar(conteudo), "o contrato OpenAPI mudou");
        }
        catch (JsonException)
        {
            return (null, "o contrato OpenAPI contém JSON inválido");
        }
    }

    private static string Normalizar(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(documento.RootElement, OpcoesDeNormalizacao);
    }
}
