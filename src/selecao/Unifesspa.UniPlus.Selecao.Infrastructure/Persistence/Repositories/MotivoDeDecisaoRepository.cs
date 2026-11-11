namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Repositório do cadastro único de motivos de decisão.</summary>
public sealed class MotivoDeDecisaoRepository : IMotivoDeDecisaoRepository
{
    private readonly SelecaoDbContext _context;

    public MotivoDeDecisaoRepository(SelecaoDbContext context)
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

    public async Task<(IReadOnlyList<MotivoDeDecisao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDosMotivos filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        CursorKeysetPage<MotivoDeDecisao> page = await CursorKeyset
            .ApplyAsync(Filtrar(_context.MotivosDeDecisao.AsNoTracking(), filtro), afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<bool> CodigoExisteAsync(TipoMotivoDeDecisao tipo, string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        // Um código fora do formato nunca corresponde a motivo algum. A comparação é pelo value
        // object inteiro, e não por `.Valor`: o código é persistido por conversor, e o EF não traduz
        // o acesso à propriedade interna.
        Result<CodigoMotivoDecisao> codigoResult = CodigoMotivoDecisao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return false;
        }

        CodigoMotivoDecisao codigoVo = codigoResult.Value!;
        return await _context.MotivosDeDecisao
            .AsNoTracking()
            .AnyAsync(m => m.Tipo == tipo && m.Codigo == codigoVo, cancellationToken)
            .ConfigureAwait(false);
    }

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
}
