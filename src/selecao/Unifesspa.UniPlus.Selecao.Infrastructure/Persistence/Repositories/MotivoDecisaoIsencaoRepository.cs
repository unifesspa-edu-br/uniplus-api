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

/// <summary>Repositório do catálogo de motivos de decisão de isenção.</summary>
public sealed class MotivoDecisaoIsencaoRepository : IMotivoDecisaoIsencaoRepository
{
    private readonly SelecaoDbContext _context;

    public MotivoDecisaoIsencaoRepository(SelecaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<MotivoDecisaoIsencao?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await _context.MotivosDecisaoIsencao
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MotivoDecisaoIsencao>> ObterTodosAsync(
        CancellationToken cancellationToken = default) =>
        await _context.MotivosDecisaoIsencao
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AdicionarAsync(
        MotivoDecisaoIsencao entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.MotivosDecisaoIsencao.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Atualizar(MotivoDecisaoIsencao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.MotivosDecisaoIsencao.Update(entity);
    }

    public void Remover(MotivoDecisaoIsencao entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // O catálogo não remove: a retirada de um motivo é a desativação, que
        // preserva o que já o referencia (UNI-REQ-0122). O método existe porque
        // IRepository o declara; chamá-lo apagaria a linha que as decisões já
        // proferidas citam.
        throw new NotSupportedException(
            "Motivo de decisão de isenção não é removido — a retirada do catálogo é a desativação.");
    }

    public async Task<(IReadOnlyList<MotivoDecisaoIsencao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FundamentoIsencao? fundamento,
        bool apenasAtivos,
        CancellationToken cancellationToken = default)
    {
        // Sem OrderBy aqui: o helper keyset (ADR-0089) ordena e fatia.
        IQueryable<MotivoDecisaoIsencao> query = _context.MotivosDecisaoIsencao.AsNoTracking();

        if (fundamento is { } valor)
        {
            query = query.Where(m => m.Fundamento == valor);
        }

        if (apenasAtivos)
        {
            query = query.Where(m => m.Ativo);
        }

        CursorKeysetPage<MotivoDecisaoIsencao> page = await CursorKeyset
            .ApplyAsync(query, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.PrevAfterId, page.NextAfterId);
    }

    public async Task<bool> CodigoExisteAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        // Um código fora do formato nunca corresponde a motivo algum, e
        // construir o value object antes de consultar garante que a comparação
        // use o mesmo valor canônico que a escrita gravaria.
        Result<CodigoMotivoDecisao> codigoResult = CodigoMotivoDecisao.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return false;
        }

        CodigoMotivoDecisao codigoVo = codigoResult.Value!;

        // Comparação pelo value object inteiro, e não por `.Valor`: o código é
        // persistido por conversor, e não como tipo próprio — o EF não tem como
        // traduzir o acesso à propriedade interna e a consulta estouraria em
        // tempo de execução.
        return await _context.MotivosDecisaoIsencao
            .AsNoTracking()
            .AnyAsync(m => m.Codigo == codigoVo, cancellationToken)
            .ConfigureAwait(false);
    }
}
