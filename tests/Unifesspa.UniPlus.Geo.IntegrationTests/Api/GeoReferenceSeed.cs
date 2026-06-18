namespace Unifesspa.UniPlus.Geo.IntegrationTests.Api;

using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Semeadura determinística (chaves naturais fixas) de reference data Geo para os
/// testes de API #675. Cada teste TRUNCA antes de semear — a collection é serial
/// e compartilhada, então a janela não pode depender de estado de outro teste.
/// </summary>
internal static partial class GeoReferenceSeed
{
    public const string Versao = "202601";

    /// <summary>GET por rota relativa ou absoluta (satisfaz CA2234 — <c>GetAsync(Uri)</c>).</summary>
    public static Task<HttpResponseMessage> Obter(HttpClient client, string rota)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetAsync(new Uri(rota, UriKind.RelativeOrAbsolute));
    }

    /// <summary>Limpa toda a hierarquia Geo (TRUNCATE pais CASCADE derruba estado/cidade/indicador/faixas).</summary>
    public static async Task LimparAsync(GeoPostgisFixture fixture)
    {
        await using GeoDbContext ctx = fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE pais CASCADE");
    }

    public static Pais NovoBrasil() =>
        GeoTestKeys.Ok(Pais.Importar("BRA", "BR", "Brasil", null, null, null, null, Versao));

    public static Estado NovoEstado(
        Guid paisId,
        string uf,
        string nome,
        string? regiao = null,
        string? capital = null,
        string? codigoIbge = null,
        decimal? latitude = null,
        decimal? longitude = null,
        bool vigente = true) =>
        GeoTestKeys.Ok(Estado.Importar(
            paisId, uf, nome, Normalizar(nome), regiao, capital, codigoIbge,
            latitude, longitude, null, null, null, Versao, vigente));

    public static Cidade NovaCidade(
        Guid estadoId,
        string uf,
        string codigoIbge,
        string nome,
        string? nomeNormalizado = null,
        string? ddd = null,
        decimal? latitude = null,
        decimal? longitude = null,
        string? mesorregiaoNome = null,
        string? microrregiaoNome = null,
        string? regiaoIntermediariaNome = null,
        string? regiaoImediataNome = null,
        bool vigente = true) =>
        GeoTestKeys.Ok(Cidade.Importar(
            estadoId, uf, codigoIbge, nome,
            nomeNormalizado ?? Normalizar(nome), ddd, latitude, longitude, null,
            null, mesorregiaoNome, null, microrregiaoNome,
            null, regiaoIntermediariaNome, null, regiaoImediataNome,
            Versao, vigente));

    public static CidadeIndicador NovoIndicador(
        Guid cidadeId,
        string? gentilico = null,
        decimal? areaKm2 = null,
        int? populacaoResidente = null,
        decimal? densidadeDemografica = null,
        decimal? idh = null,
        string? aniversario = null) =>
        GeoTestKeys.Ok(CidadeIndicador.Importar(
            cidadeId, gentilico, null, areaKm2, populacaoResidente, densidadeDemografica,
            null, idh, null, null, null, null, aniversario, Versao));

    /// <summary>Extrai a URL de um <c>rel</c> específico do header <c>Link</c> (RFC 5988/8288), ou <see langword="null"/>.</summary>
    public static string? ExtrairLink(HttpResponseMessage resposta, string rel)
    {
        if (!resposta.Headers.TryGetValues("Link", out IEnumerable<string>? values))
        {
            return null;
        }

        string header = string.Join(", ", values);
        foreach (Match match in LinkHeaderRegex().Matches(header))
        {
            if (string.Equals(match.Groups["rel"].Value, rel, StringComparison.Ordinal))
            {
                return match.Groups["url"].Value;
            }
        }

        return null;
    }

    // Remove os diacríticos (como a fonte faz em *_sem_acento). O ToLower fica por
    // conta da própria entidade (GeoTexto.NormalizarBuscaOpcional) — evita CA1308 aqui.
    private static string Normalizar(string nome)
    {
        string decomposto = nome.Normalize(NormalizationForm.FormD);
        return new string([.. decomposto.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    [GeneratedRegex("<(?<url>[^>]+)>;\\s*rel=\"(?<rel>[^\"]+)\"")]
    private static partial Regex LinkHeaderRegex();
}
