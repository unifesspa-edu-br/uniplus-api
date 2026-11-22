namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json.Serialization;

using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// A regra de deliberação que o edital declara para uma banca: composição e campos da votação.
/// A votação colegiada vale para todas as decisões; no ato individual, pode ser declarada para o julgamento do recurso,
/// com quórum, critério e encerramento juntos, ou nenhum deles.
/// </summary>
public sealed record RegraDeDeliberacaoInput
{
    [JsonRequired]
    [AceitaCodigosDe<ModoDeDeliberacao>]
    public string? Modo { get; init; }

    [JsonRequired]
    public int Integrantes { get; init; }

    [JsonRequired]
    public int Suplentes { get; init; }

    public int? Quorum { get; init; }

    [AceitaCodigosDe<CriterioDeVotacao>]
    public string? Criterio { get; init; }

    [AceitaCodigosDe<DesempateDaVotacao>]
    public string? Desempate { get; init; }

    [AceitaCodigosDe<EncerramentoDaVotacao>]
    public string? Encerramento { get; init; }

    [AceitaCodigosDe<ResultadoSemUnanimidade>]
    public string? SemUnanimidade { get; init; }

    internal DeclaracaoDaDeliberacao ParaDeclaracao() =>
        new(Modo, Integrantes, Suplentes, new(Quorum, Criterio, Desempate, Encerramento, SemUnanimidade));
}
