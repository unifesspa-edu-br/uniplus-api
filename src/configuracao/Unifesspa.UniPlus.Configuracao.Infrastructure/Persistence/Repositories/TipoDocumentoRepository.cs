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
public sealed class TipoDocumentoRepository : ITipoDocumentoRepository
{
    private readonly ConfiguracaoDbContext _dbContext;

    public TipoDocumentoRepository(ConfiguracaoDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<TipoDocumento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposDocumento
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<TipoDocumento?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.TiposDocumento
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<TipoDocumento> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<TipoDocumento> page = await CursorKeyset
            .ApplyAsync(_dbContext.TiposDocumento.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task AdicionarAsync(TipoDocumento tipo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        await _dbContext.TiposDocumento.AddAsync(tipo, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        _dbContext.TiposDocumento.Remove(tipo);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Código fora do formato não pode existir na tabela: o value object recusa
        // a escrita e o CHECK recusa o insert cru. Responder "não existe" sem ir ao
        // banco evita uma consulta que só poderia voltar vazia — e evita converter
        // um valor inválido, o que estouraria no conversor.
        Result<CodigoTipoDocumento> codigoResult = CodigoTipoDocumento.Criar(codigo);
        if (codigoResult.IsFailure || codigoResult.Value is null)
        {
            return Task.FromResult(false);
        }

        // Comparação entre value objects: o conversor traduz para varchar e a
        // comparação é case-sensitive (default do Postgres), alinhada ao índice único.
        CodigoTipoDocumento codigoVo = codigoResult.Value;

        return _dbContext.TiposDocumento
            .AsNoTracking()
            .Where(t => excluirId == null || t.Id != excluirId)
            .AnyAsync(t => t.Codigo == codigoVo, cancellationToken);
    }
}
