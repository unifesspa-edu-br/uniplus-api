namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lê a unidade da fase na configuração congelada da versão corrente do processo, a de maior número na cadeia. A
/// consulta projeta só o texto do envelope, sem os bytes canônicos, e herda o filtro de exclusão lógica do processo.
/// </summary>
internal sealed class UnidadeDaFaseNaVersaoVigente : IUnidadeDaFaseNaVersaoVigente
{
    private readonly SelecaoDbContext _context;

    public UnidadeDaFaseNaVersaoVigente(SelecaoDbContext context) => _context = context;

    public async Task<UnidadeDaFase?> DaFaseAsync(Guid processoId, Guid faseId, CancellationToken cancellationToken = default) =>
        FasesNoEnvelope.DaFase(await EnvelopeVigenteAsync(processoId, cancellationToken).ConfigureAwait(false), faseId);

    public async Task<UnidadeDaFase?> DaEtapaAsync(Guid processoId, Guid etapaId, CancellationToken cancellationToken = default) =>
        FasesNoEnvelope.DaEtapa(await EnvelopeVigenteAsync(processoId, cancellationToken).ConfigureAwait(false), etapaId);

    private Task<string?> EnvelopeVigenteAsync(Guid processoId, CancellationToken cancellationToken) =>
        _context.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId
                && _context.ProcessosSeletivos.Any(p => p.Id == processoId))
            .OrderByDescending(v => v.NumeroVersao)
            .Select(v => v.ConfiguracaoCongelada)
            .FirstOrDefaultAsync(cancellationToken);
}
