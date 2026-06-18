namespace Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Indicadores socioeconômicos de uma <c>Cidade</c> (satélite 1:1), embutidos no
/// <see cref="CidadeDetalheDto"/>. Todos os campos são <c>nullable</c> — a fonte
/// IBGE traz <c>'-'</c> para parte dos municípios (parse tolerante degrada para
/// <see langword="null"/>). <c>Aniversario</c> é <c>string</c> "DD/MM" (sem ano,
/// não é data). Não carrega <c>_links</c> (é embutido, não recurso navegável).
/// </summary>
public sealed record CidadeIndicadorDto(
    string? Gentilico,
    decimal? AreaKm2,
    int? PopulacaoResidente,
    decimal? DensidadeDemografica,
    decimal? Idh,
    string? Aniversario);
