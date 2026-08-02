namespace Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// DTO read-only do dataset vigente de calendário de dias úteis, para consumo
/// cross-módulo via <see cref="ICalendarioVigenteReader"/> (ADR-0056). O motor de
/// contagem de dias úteis (módulo de Recursos, escopo futuro) resolve um
/// instante-âncora contra <see cref="Datas"/> para decidir se cai em dia não útil.
/// </summary>
/// <param name="Id">Identificador do dataset (Guid v7 — ADR-0032).</param>
/// <param name="VersaoDataset">Versão do dataset vigente.</param>
/// <param name="Datas">Todas as datas não úteis do dataset vigente, sem distinção de abrangência/município.</param>
public sealed record CalendarioVigenteView(
    Guid Id,
    string VersaoDataset,
    IReadOnlyList<DateOnly> Datas);
