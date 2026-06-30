namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class CondicaoAtendimentoRepository : ICondicaoAtendimentoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public CondicaoAtendimentoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<CondicaoAtendimentoEspecializado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CondicoesAtendimento
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public Task<CondicaoAtendimentoEspecializado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.CondicoesAtendimento
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<CondicaoAtendimentoEspecializado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<CondicaoAtendimentoEspecializado> page = await CursorKeyset
            .ApplyAsync(_dbContext.CondicoesAtendimento.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(CondicaoAtendimentoEspecializado condicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(condicao);
        await _dbContext.CondicoesAtendimento.AddAsync(condicao, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(CondicaoAtendimentoEspecializado condicao)
    {
        ArgumentNullException.ThrowIfNull(condicao);
        _dbContext.CondicoesAtendimento.Remove(condicao);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato nunca tem condição viva — evita query
        // desnecessária e garante que a comparação use o valor canônico normalizado
        // (Trim). Case-sensitive (default do Postgres) — alinhado ao índice único.
        Result<CodigoCondicao> codigoResult = CodigoCondicao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        CodigoCondicao codigoVo = codigoResult.Value!;

        return _dbContext.CondicoesAtendimento
            .AsNoTracking()
            .Where(c => excluirId == null || c.Id != excluirId)
            .AnyAsync(c => c.Codigo == codigoVo, cancellationToken);
    }
}
