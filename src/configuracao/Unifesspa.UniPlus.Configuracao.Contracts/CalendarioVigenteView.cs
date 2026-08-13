namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only do dataset vigente de calendário de dias úteis, para consumo
/// cross-módulo via <see cref="ICalendarioVigenteReader"/> (ADR-0056). O motor de
/// contagem de dias úteis (módulo de Recursos, escopo futuro) resolve um
/// instante-âncora contra <see cref="DiasNaoUteis"/> para decidir se cai em dia
/// não útil — inclusive quando a abrangência é municipal/estadual e só se aplica a
/// uma localidade específica.
/// </summary>
/// <param name="Id">Identificador do dataset (Guid v7 — ADR-0032).</param>
/// <param name="VersaoDataset">Versão do dataset vigente.</param>
/// <param name="DiasNaoUteis">Todos os dias não úteis do dataset vigente, com abrangência e município preservados.</param>
public sealed record CalendarioVigenteView(
    Guid Id,
    string VersaoDataset,
    IReadOnlyList<DiaNaoUtilView> DiasNaoUteis);

/// <summary>
/// Um dia não útil do dataset vigente, projetado para consumo cross-módulo.
/// </summary>
/// <param name="Data">A data não útil.</param>
/// <param name="Abrangencia">Token canônico UPPER_SNAKE (ex.: <c>NACIONAL</c>, <c>MUNICIPAL</c>) — determina onde a data se aplica.</param>
/// <param name="MunicipioIbge">Código IBGE do município, presente apenas quando <see cref="Abrangencia"/> é <c>MUNICIPAL</c>.</param>
/// <param name="MunicipioNome">Nome do município, presente apenas quando <see cref="Abrangencia"/> é <c>MUNICIPAL</c>.</param>
/// <param name="MunicipioUf">UF do município, presente apenas quando <see cref="Abrangencia"/> é <c>MUNICIPAL</c>.</param>
/// <param name="Uf">UF (2 letras), presente apenas quando <see cref="Abrangencia"/> é <c>ESTADUAL</c>.</param>
public sealed record DiaNaoUtilView(
    DateOnly Data,
    string Abrangencia,
    string? MunicipioIbge,
    string? MunicipioNome,
    string? MunicipioUf,
    string? Uf);
