namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Público de propósito, como <see cref="RascunhoDePublicacaoRepository"/>: o codegen do Wolverine não instancia um
/// concreto <c>internal</c> sob <c>ServiceLocationPolicy.NotAllowed</c> sem o opt-in da ADR-0098.
/// </summary>
public sealed class RascunhoDeProcessoSeletivoRepository : IRascunhoDeProcessoSeletivoRepository
{
    private readonly UniPlusDbContext _context;
    private readonly BloqueioDeLinhaComEsperaLimitada _bloqueio;

    public RascunhoDeProcessoSeletivoRepository(UniPlusDbContext context, IOptions<TravaDoProcessoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(opcoes);

        _context = context;
        _bloqueio = new BloqueioDeLinhaComEsperaLimitada(context, opcoes.Value);
    }

    public Task<RascunhoDeProcessoSeletivo?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.RascunhosDeProcessosSeletivos
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<RascunhoDeProcessoSeletivo?> ObterParaMutacaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _bloqueio.AdquirirAsync(InstrucaoDeBloqueio.RascunhoExclusivo, id, cancellationToken).ConfigureAwait(false);

        return await _context.RascunhosDeProcessosSeletivos
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> BloquearCompartilhadoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _bloqueio.AdquirirAsync(InstrucaoDeBloqueio.RascunhoCompartilhado, id, cancellationToken).ConfigureAwait(false);

        return await _context.RascunhosDeProcessosSeletivos
            .AsNoTracking()
            .AnyAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(RascunhoDeProcessoSeletivo rascunho, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rascunho);
        await _context.RascunhosDeProcessosSeletivos.AddAsync(rascunho, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(RascunhoDeProcessoSeletivo rascunho)
    {
        ArgumentNullException.ThrowIfNull(rascunho);
        _context.RascunhosDeProcessosSeletivos.Remove(rascunho);
    }
}
