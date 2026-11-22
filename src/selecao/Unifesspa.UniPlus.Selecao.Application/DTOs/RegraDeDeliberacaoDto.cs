namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Domain.Enums;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>Projeção de leitura de <c>RegraDeDeliberacao</c>: como a banca é composta e delibera.</summary>
public sealed record RegraDeDeliberacaoDto
{
    public required CodigoComRotulo<ModoDeDeliberacao> Modo { get; init; }
    public required int Integrantes { get; init; }
    public required int Suplentes { get; init; }

    /// <summary>Quórum da votação do colegiado ou da votação do recurso no ato individual, quando declarada.</summary>
    public required int? Quorum { get; init; }

    /// <summary>Critério da votação do colegiado ou da votação do recurso no ato individual, quando declarada.</summary>
    public required CodigoComRotulo<CriterioDeVotacao>? Criterio { get; init; }

    /// <summary>Somente nos critérios de maioria, em banca com mais de um integrante.</summary>
    public required CodigoComRotulo<DesempateDaVotacao>? Desempate { get; init; }

    /// <summary>Encerramento da votação do colegiado ou da votação do recurso no ato individual, quando declarada.</summary>
    public required CodigoComRotulo<EncerramentoDaVotacao>? Encerramento { get; init; }

    /// <summary>Somente no critério de unanimidade, em banca com mais de um integrante.</summary>
    public required CodigoComRotulo<ResultadoSemUnanimidade>? SemUnanimidade { get; init; }
}
