namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Citação normativa de uma regra legal: o texto que a explica, a base legal e as referências opcionais ao ato.
/// Carrega o que o cliente enviou, ainda sem validar, para que a validação do lote acumule as violações do
/// texto junto com as dos demais campos. <paramref name="AtoNormativo"/> é texto livre (DOI, URN, IRI ou
/// endereço), preservado como informado.
/// </summary>
public sealed record TextoNormativo(
    string? DescricaoHumana,
    string? BaseLegal,
    string? AtoNormativo,
    string? PortariaInternaCodigo);
