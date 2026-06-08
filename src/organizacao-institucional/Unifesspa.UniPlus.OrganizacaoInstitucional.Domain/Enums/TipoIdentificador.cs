namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Tipo do identificador cujo histórico está sendo registrado em
/// <c>UnidadeIdentificadorHistorico</c>. Slug, Sigla e Codigo são únicos entre
/// Unidades vivas; Alias não é único.
/// </summary>
public enum TipoIdentificador
{
    Nenhum = 0,
    Slug = 1,
    Sigla = 2,
    Codigo = 3,
    Alias = 4,
}
