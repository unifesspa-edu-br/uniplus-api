namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

/// <summary>Projeção de leitura de <c>RegraDeDeliberacao</c>: como a banca é composta e delibera.</summary>
/// <param name="Quorum">Só na votação do colegiado.</param>
/// <param name="Criterio">Só na votação do colegiado.</param>
/// <param name="Desempate">Só nos critérios de maioria, em banca com mais de 1 integrante.</param>
/// <param name="Encerramento">Só na votação do colegiado.</param>
/// <param name="SemUnanimidade">Só no critério de unanimidade, em banca com mais de 1 integrante.</param>
public sealed record RegraDeDeliberacaoDto(
    ModoDeDeliberacao Modo,
    int Integrantes,
    int Suplentes,
    int? Quorum,
    CriterioDeVotacao? Criterio,
    DesempateDaVotacao? Desempate,
    EncerramentoDaVotacao? Encerramento,
    ResultadoSemUnanimidade? SemUnanimidade);
