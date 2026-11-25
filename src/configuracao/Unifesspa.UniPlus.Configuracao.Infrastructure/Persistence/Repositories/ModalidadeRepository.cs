namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ModalidadeRepository : IModalidadeRepository
{
    private readonly UniPlusDbContext _dbContext;

    public ModalidadeRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public Task<Modalidade?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Modalidades
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<Modalidade?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Modalidades
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Modalidade> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        // Keyset bidirecional (ADR-0089): ordenação por Id (Guid v7, ADR-0026/0032).
        CursorKeysetPage<Modalidade> page = await CursorKeyset
            .ApplyAsync(_dbContext.Modalidades.AsNoTracking(), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<IReadOnlyList<Modalidade>> ListarVivasAsync(CancellationToken cancellationToken) =>
        await _dbContext.Modalidades.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(Modalidade modalidade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        await _dbContext.Modalidades.AddAsync(modalidade, cancellationToken).ConfigureAwait(false);
    }

    public void Remover(Modalidade modalidade)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        _dbContext.Modalidades.Remove(modalidade);
    }

    public Task<bool> CodigoExisteEntreVivosAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        // Um código fora do formato não identifica modalidade viva.
        Result<CodigoModalidade> codigoResult = CodigoModalidade.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Task.FromResult(false);
        }

        return CodigoExisteEntreVivosAsync(codigoResult.Value!, excluirId, cancellationToken);
    }

    private Task<bool> CodigoExisteEntreVivosAsync(CodigoModalidade codigo, Guid? excluirId, CancellationToken cancellationToken) =>
        _dbContext.Modalidades
            .AsNoTracking()
            .Where(m => excluirId == null || m.Id != excluirId)
            .AnyAsync(m => m.Codigo == codigo, cancellationToken);

    public async Task<bool> CodigosVivosExistemAsync(
        IReadOnlyCollection<string> codigos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        List<string> distintos = CodigosDistintos(codigos);
        if (!ConverterCodigos(distintos, out List<CodigoModalidade> codigosVo))
        {
            return false;
        }

        return codigosVo.Count == 0 || await ContarCodigosVivosAsync(codigosVo, cancellationToken).ConfigureAwait(false) == codigosVo.Count;
    }

    private static List<string> CodigosDistintos(IReadOnlyCollection<string> codigos) => codigos
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool ConverterCodigos(List<string> codigos, out List<CodigoModalidade> convertidos)
    {
        convertidos = new List<CodigoModalidade>(codigos.Count);
        foreach (string codigo in codigos)
        {
            Result<CodigoModalidade> resultado = CodigoModalidade.Criar(codigo);
            if (resultado.IsFailure)
            {
                return false;
            }

            convertidos.Add(resultado.Value!);
        }

        return true;
    }

    private Task<int> ContarCodigosVivosAsync(List<CodigoModalidade> codigos, CancellationToken cancellationToken) =>
        _dbContext.Modalidades
            .AsNoTracking()
            .Where(m => codigos.Contains(m.Codigo))
            .Select(m => m.Codigo)
            .Distinct()
            .CountAsync(cancellationToken);

    public Task<bool> EhReferenciadaPorOutraModalidadeVivaAsync(
        string codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        IQueryable<Modalidade> consulta = ReferenciasAoCodigo(codigo);
        if (excluirId is { } id)
        {
            consulta = consulta.Where(m => m.Id != id);
        }

        return consulta.AnyAsync(cancellationToken);
    }

    private IQueryable<Modalidade> ReferenciasAoCodigo(string codigo) =>
        _dbContext.Modalidades.AsNoTracking().Where(m =>
            m.ComposicaoOrigem == codigo || m.RemanejamentoParametros.Destino == codigo ||
            m.RemanejamentoParametros.Par == codigo || m.RemanejamentoParametros.DestinoFinal == codigo);
}
