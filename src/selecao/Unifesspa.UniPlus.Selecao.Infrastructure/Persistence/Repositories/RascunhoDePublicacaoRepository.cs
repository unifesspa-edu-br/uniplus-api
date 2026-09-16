namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Domain.Entities;
using Domain.Interfaces;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Público de propósito, como <see cref="DocumentoEditalRepository"/>: o codegen do Wolverine
/// não consegue instanciar um concreto <c>internal</c> sob <c>ServiceLocationPolicy.NotAllowed</c>
/// e exigiria o opt-in da ADR-0098. Sendo público, o handler o recebe sem cerimônia nenhuma.
/// </summary>
public sealed class RascunhoDePublicacaoRepository : IRascunhoDePublicacaoRepository
{
    private readonly SelecaoDbContext _context;

    public RascunhoDePublicacaoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<RascunhoDePublicacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RascunhoDePublicacao>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RascunhoDePublicacao?> ObterDoOperadorAsync(
        Guid processoSeletivoId,
        string usuarioSub,
        CancellationToken cancellationToken = default)
    {
        // Rastreado: quem lê ou grava de novo (substituindo o conteúdo) ou apaga (expirado na
        // leitura), e os dois precisam da entidade sob o change tracker.
        return await _context.RascunhosDePublicacao
            .FirstOrDefaultAsync(
                r => r.ProcessoSeletivoId == processoSeletivoId && r.UsuarioSub == usuarioSub,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ApagarDoProcessoAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .Where(r => r.ProcessoSeletivoId == processoSeletivoId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ApagarVencidosAsync(DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .Where(r => r.ExpiraEm <= agora)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ApagarDoOperadorAsync(
        Guid processoSeletivoId,
        string usuarioSub,
        CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .Where(r => r.ProcessoSeletivoId == processoSeletivoId && r.UsuarioSub == usuarioSub)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> ApagarSeVencidoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default)
    {
        return await _context.RascunhosDePublicacao
            .Where(r => r.Id == id && r.ExpiraEm <= agora)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AdicionarAsync(RascunhoDePublicacao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.RascunhosDePublicacao.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(RascunhoDePublicacao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.RascunhosDePublicacao.Update(entity);
    }

    public void Remover(RascunhoDePublicacao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.RascunhosDePublicacao.Remove(entity);
    }
}
