namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Resolve o fuso institucional de verdade, contra a base de fusos do runner. Não é mock: o valor
/// entra no envelope congelado, e um dublê devolvendo zona arbitrária deixaria passar teste que a
/// canonicalização real recusaria.
/// </summary>
internal sealed class ResolvedorFusoDeTeste : IResolvedorFusoInstitucional
{
    public Result<TimeZoneInfo> Resolver() =>
        Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId));
}

/// <summary>Simula ambiente cuja base de fusos não reconhece a zona institucional.</summary>
internal sealed class ResolvedorFusoIndisponivelDeTeste : IResolvedorFusoInstitucional
{
    public Result<TimeZoneInfo> Resolver() =>
        Result<TimeZoneInfo>.Failure(new DomainError(
            "ProcessoSeletivo.FusoInstitucionalNaoReconhecido",
            $"O fuso institucional '{FusoInstitucional.ZoneId}' não é reconhecido pela base de fusos deste ambiente."));
}
