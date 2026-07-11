namespace Unifesspa.UniPlus.Publicacoes.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta para <c>AtoNormativo</c>. Expõe a essência documental do ato,
/// os atributos de consequência copiados por valor do catálogo, — quando o ato
/// invoca configuração — o par <c>{id, hash}</c> da versão que o governou, e — quando
/// o ato emenda outro — a linhagem de retificação (<c>AtoRetificadoId</c> + motivo).
/// </summary>
/// <remarks>
/// <para><c>DataPublicacao</c> é documental (o que o PDF declara);
/// <c>RegistradoEm</c> é o instante forense em que o registro entrou no sistema.
/// São grandezas distintas — só a segunda ordena no relógio do sistema.</para>
/// <para><c>AtoRetificadoId</c>/<c>MotivoRetificacao</c> são o par simétrico da
/// retificação (ADR-0103): ambos presentes num ato que emenda outro, ambos nulos
/// no ato que não emenda ninguém.</para>
/// <para><c>Avisos</c> é nulo (omitido) na listagem, para evitar N+1; no detalhe
/// (GET por id) é recomputado — lista vazia significa "sem colisão de número".</para>
/// <para>Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029).</para>
/// </remarks>
public sealed record AtoNormativoDto(
    Guid Id,
    string Orgao,
    string Serie,
    int Ano,
    string? Numero,
    string TipoCodigo,
    bool CongelaConfiguracao,
    bool EfeitoIrreversivel,
    bool UnicoPorObjeto,
    DateOnly DataPublicacao,
    string DocumentoHash,
    string Assinante,
    DateTimeOffset RegistradoEm,
    Guid? VersaoInvocadaId,
    string? VersaoInvocadaHash,
    Guid? AtoRetificadoId,
    string? MotivoRetificacao)
{
    /// <summary>
    /// Avisos de numeração recomputados na leitura. Nulo quando não computados
    /// (listagem); lista possivelmente vazia no detalhe do ato.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AvisoNumeracao>? Avisos { get; init; }

    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
