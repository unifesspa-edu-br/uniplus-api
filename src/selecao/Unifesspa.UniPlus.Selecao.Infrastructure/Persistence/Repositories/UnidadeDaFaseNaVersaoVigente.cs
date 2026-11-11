namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê a unidade da fase na configuração congelada da versão vigente do processo (<see cref="VersoesVigentes"/>). A
/// consulta projeta só o texto do envelope, sem os bytes canônicos, e herda o filtro de exclusão lógica do processo.
/// </summary>
internal sealed class UnidadeDaFaseNaVersaoVigente : IUnidadeDaFaseNaVersaoVigente
{
    private readonly SelecaoDbContext _context;
    private readonly TimeProvider _timeProvider;

    public UnidadeDaFaseNaVersaoVigente(SelecaoDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<UnidadeDaFase?> DaFaseAsync(Guid processoId, Guid faseId, CancellationToken cancellationToken = default) =>
        FasesNoEnvelope.DaFase(await EnvelopeVigenteAsync(processoId, cancellationToken).ConfigureAwait(false), faseId);

    public async Task<UnidadeDaFase?> DaEtapaAsync(Guid processoId, Guid etapaId, CancellationToken cancellationToken = default) =>
        FasesNoEnvelope.DaEtapa(await EnvelopeVigenteAsync(processoId, cancellationToken).ConfigureAwait(false), etapaId);

    private Task<string?> EnvelopeVigenteAsync(Guid processoId, CancellationToken cancellationToken) =>
        _context.Vigentes(processoId, _timeProvider.GetUtcNow())
            .AsNoTracking()
            .Select(v => v.ConfiguracaoCongelada)
            .FirstOrDefaultAsync(cancellationToken);
}
