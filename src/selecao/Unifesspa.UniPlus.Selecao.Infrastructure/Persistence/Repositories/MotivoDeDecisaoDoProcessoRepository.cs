namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Repositório dos motivos escolhidos pelos processos.</summary>
public sealed class MotivoDeDecisaoDoProcessoRepository : IMotivoDeDecisaoDoProcessoRepository
{
    private readonly UniPlusDbContext _context;

    public MotivoDeDecisaoDoProcessoRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyList<MotivoDeDecisaoDoProcesso>> ListarEscolhaAtualAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        await EscolhaAtual(processoSeletivoId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MotivoEscolhidoComOrigem>> ListarEscolhaAtualComOrigemAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        List<MotivoDeDecisaoDoProcesso> escolhidos = await EscolhaAtual(processoSeletivoId).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        Guid[] origens = [.. escolhidos.Select(static e => e.MotivoDeDecisaoOrigemId)];

        Dictionary<Guid, MotivoDeDecisao> porId = await _context.MotivosDeDecisao
            .AsNoTracking()
            .Where(m => origens.Contains(m.Id))
            .ToDictionaryAsync(static m => m.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. escolhidos.Select(e => new MotivoEscolhidoComOrigem(e, porId.GetValueOrDefault(e.MotivoDeDecisaoOrigemId)))];
    }

    private IQueryable<MotivoDeDecisaoDoProcesso> EscolhaAtual(Guid processoSeletivoId) =>
        _context.MotivosDeDecisaoDoProcesso
            .Where(m => m.ProcessoSeletivoId == processoSeletivoId)
            .Where(MotivoDeDecisaoDoProcesso.NaEscolhaAtual);

    public async Task<IReadOnlyList<MotivoDeDecisaoDoProcesso>?> ListarDaVersaoPublicadaAsync(
        Guid processoSeletivoId, int numeroDaVersao, CancellationToken cancellationToken = default)
    {
        bool publicada = await _context.VersoesConfiguracao
            .AnyAsync(v => v.ProcessoSeletivoId == processoSeletivoId && v.NumeroVersao == numeroDaVersao, cancellationToken)
            .ConfigureAwait(false);

        return publicada
            ? await _context.MotivosDeDecisaoDoProcesso
                .AsNoTracking()
                .Where(m => m.ProcessoSeletivoId == processoSeletivoId)
                .Where(MotivoDeDecisaoDoProcesso.ValeNaVersao(numeroDaVersao))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
    }

    public async Task AdicionarAsync(MotivoDeDecisaoDoProcesso motivo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(motivo);
        await _context.MotivosDeDecisaoDoProcesso.AddAsync(motivo, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(MotivoDeDecisaoDoProcesso motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);
        _context.MotivosDeDecisaoDoProcesso.Remove(motivo);
    }
}
