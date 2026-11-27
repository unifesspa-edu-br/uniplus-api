namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Repositório do cadastro único de motivos de decisão.</summary>
public sealed class MotivoDeDecisaoRepository : IMotivoDeDecisaoRepository
{
    private readonly UniPlusDbContext _context;

    public MotivoDeDecisaoRepository(UniPlusDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<MotivoDeDecisao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.MotivosDeDecisao.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MotivoDeDecisao>> ObterTodosAsync(CancellationToken cancellationToken = default) =>
        await _context.MotivosDeDecisao.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AdicionarAsync(MotivoDeDecisao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.MotivosDeDecisao.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(MotivoDeDecisao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.MotivosDeDecisao.Update(entity);
    }

    public void Remover(MotivoDeDecisao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // O cadastro não remove: a retirada de um motivo é a desativação, que preserva o que já o
        // escolheu (UNI-REQ-0122). O método existe porque IRepository o declara.
        throw new NotSupportedException("Motivo de decisão não é removido — a retirada do cadastro é a desativação.");
    }

    public async Task<(IReadOnlyList<MotivoDeDecisao> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDosMotivos filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        OrderedKeysetPage<MotivoDeDecisao> page = await CursorKeyset
            .ApplyAsync(Filtrar(_context.MotivosDeDecisao.AsNoTracking(), filtro), CondicoesDe(filtro), afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task<IReadOnlyList<MotivoDeDecisao>> ListarVivosPorCodigosAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        CodigoMotivoDecisao[] validos = [.. codigos.Select(static par => CodigoMotivoDecisao.Criar(par.Codigo))
            .Where(static resultado => resultado.IsSuccess).Select(static resultado => resultado.Value!)];
        List<MotivoDeDecisao> encontrados = await _context.MotivosDeDecisao.AsNoTracking()
            .Where(motivo => motivo.Ativo && validos.Contains(motivo.Codigo)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. encontrados.Where(motivo => codigos.Contains((motivo.Codigo.Valor, motivo.Tipo.Codigo())))];
    }

    public async Task<bool> CodigoExisteAsync(TipoMotivoDeDecisao tipo, string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        // Um código fora do formato nunca corresponde a motivo algum.
        Result<CodigoMotivoDecisao> codigoResult = CodigoMotivoDecisao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return false;
        }

        return await ExisteMotivoDoTipoComCodigoAsync(tipo, codigoResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    // A comparação é pelo value object inteiro, e não por `.Valor`: o código é persistido por
    // conversor, e o EF não traduz o acesso à propriedade interna.
    private async Task<bool> ExisteMotivoDoTipoComCodigoAsync(
        TipoMotivoDeDecisao tipo,
        CodigoMotivoDecisao codigo,
        CancellationToken cancellationToken) =>
        await _context.MotivosDeDecisao
            .AsNoTracking()
            .AnyAsync(m => m.Tipo == tipo && m.Codigo == codigo, cancellationToken)
            .ConfigureAwait(false);

    private static IQueryable<MotivoDeDecisao> Filtrar(IQueryable<MotivoDeDecisao> query, FiltroDosMotivos filtro)
    {
        if (filtro.Tipo is { } tipo)
        {
            query = query.Where(m => m.Tipo == tipo);
        }

        if (filtro.Subclassificacao is { } subclassificacao)
        {
            query = query.Where(m => m.Subclassificacao == subclassificacao);
        }

        return filtro.ApenasAtivos ? query.Where(m => m.Ativo) : query;
    }

    private static CondicoesDoFiltro CondicoesDe(FiltroDosMotivos filtro) =>
        CondicoesDoFiltro.De(
            ParteDoFiltro.De(filtro.Tipo),
            ParteDoFiltro.De(filtro.Subclassificacao),
            ParteDoFiltro.De(filtro.ApenasAtivos));
}
