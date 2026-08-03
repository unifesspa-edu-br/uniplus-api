namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em ConfiguracaoInfrastructureRegistration.")]
public sealed class TermoConsentimentoRepository : ITermoConsentimentoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TermoConsentimentoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TermoConsentimento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TermosConsentimento
            .Include(t => t.Versoes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TermoConsentimento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TermosConsentimento
            .AsNoTracking()
            .Include(t => t.Versoes)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TermoConsentimento> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        // Sem Include de Versoes — a listagem projeta só o cabeçalho do termo.
        CursorKeysetPage<TermoConsentimento> page = await CursorKeyset
            .ApplyAsync(_dbContext.TermosConsentimento.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TermoConsentimento termo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termo);
        await _dbContext.TermosConsentimento.AddAsync(termo, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarVersaoAsync(TermoConsentimento termo, TermoConsentimentoVersao versao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentNullException.ThrowIfNull(versao);

        // Nenhum campo do próprio TermoConsentimento muda ao promover — sem marcar
        // uma propriedade real como modificada, o EF Core não emite UPDATE nenhum
        // para essa linha, e o token de concorrência otimista (xmin) nunca é
        // conferido. RevisadoEm é reescrito com o MESMO valor só para forçar o
        // UPDATE amarrado ao xmin lido na consulta.
        _dbContext.Entry(termo).Property(nameof(TermoConsentimento.RevisadoEm)).IsModified = true;

        await _dbContext.VersoesTermoConsentimento.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TermoConsentimento termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        _dbContext.TermosConsentimento.Remove(termo);
    }
}
