namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>TermoConsentimento</c>, com a lista completa de
/// versões promovidas — usado pelo <c>ObterPorId</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
/// <remarks>
/// Sem <c>RevisadoPor</c> (nem <see cref="TermoConsentimentoVersaoDto.PromovidaPor"/>
/// na versão): o endpoint de leitura é anônimo (<c>GET</c> público, mesmo padrão dos
/// demais cadastros do módulo), e esses campos carregam o identificador de
/// autenticação do operador (<c>sub</c> do Keycloak) — expô-los deixaria qualquer
/// chamador não autenticado colher identificadores estáveis de administradores. O
/// ator continua gravado no domínio/banco para rastreabilidade; só não sai na
/// representação pública.
/// </remarks>
public sealed record TermoConsentimentoDto(
    Guid Id,
    string Nome,
    string? TextoRascunho,
    string? BaseLegalRascunho,
    string FormaAceiteRascunho,
    bool Revisado,
    DateTimeOffset? RevisadoEm,
    IReadOnlyList<TermoConsentimentoVersaoDto> Versoes,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
