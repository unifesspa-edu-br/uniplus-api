namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere que as inscrições do processo estão abertas no instante, pela janela da versão divulgada.</summary>
public sealed class PeriodoDaInscricao(
    ICertameDivulgadoRepository certames,
    IProcessoSeletivoRepository processos,
    IRegistroCodecsEnvelope codecs,
    IResolvedorFusoInstitucional fuso)
{
    public Task<DomainError?> ConferirAsync(Guid processoSeletivoId, DateTimeOffset instante, CancellationToken cancellationToken) =>
        PeriodoDaInscricaoResolver.ConferirAsync(processoSeletivoId, certames, processos, codecs, fuso, instante, cancellationToken);
}
