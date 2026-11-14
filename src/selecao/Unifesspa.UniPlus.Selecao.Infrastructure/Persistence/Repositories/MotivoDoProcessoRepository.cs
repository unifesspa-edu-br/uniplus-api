namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Repositório dos motivos escolhidos pelos processos.</summary>
public sealed class MotivoDoProcessoRepository : IMotivoDoProcessoRepository
{
    private readonly UniPlusDbContext _context;

    public MotivoDoProcessoRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyList<MotivoDoProcesso>> ListarDoProcessoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default) =>
        await _context.MotivosDoProcesso
            .Where(m => m.ProcessoSeletivoId == processoSeletivoId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MotivoEscolhidoComOrigem>> ListarComOrigemAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MotivoDoProcesso> escolhidos = await ListarDoProcessoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        Guid[] origens = [.. escolhidos.Select(static e => e.MotivoDeDecisaoOrigemId)];

        Dictionary<Guid, MotivoDeDecisao> porId = await _context.MotivosDeDecisao
            .AsNoTracking()
            .Where(m => origens.Contains(m.Id))
            .ToDictionaryAsync(static m => m.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. escolhidos.Select(e => new MotivoEscolhidoComOrigem(e, porId.GetValueOrDefault(e.MotivoDeDecisaoOrigemId)))];
    }

    public async Task AdicionarAsync(MotivoDoProcesso motivo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(motivo);
        await _context.MotivosDoProcesso.AddAsync(motivo, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(MotivoDoProcesso motivo)
    {
        ArgumentNullException.ThrowIfNull(motivo);
        _context.MotivosDoProcesso.Remove(motivo);
    }
}
