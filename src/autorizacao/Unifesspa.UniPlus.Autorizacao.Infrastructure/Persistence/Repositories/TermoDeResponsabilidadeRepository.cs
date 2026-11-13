namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Repositório do termo de responsabilidade. A trava vem antes da leitura rastreada, que então enxerga o que a transação
/// anterior confirmou: em READ COMMITTED, quem esperou a trava relê a linha nova.
/// </summary>
public sealed class TermoDeResponsabilidadeRepository : ITermoDeResponsabilidadeRepository
{
    private const string TravaCompartilhada =
        $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.termos_de_responsabilidade WHERE id = {{0}} FOR SHARE";

    private const string TravaExclusiva =
        $"SELECT 1 FROM {AutorizacaoDbContext.Schema}.termos_de_responsabilidade WHERE id = {{0}} FOR UPDATE";

    private readonly AutorizacaoDbContext _context;

    public TermoDeResponsabilidadeRepository(AutorizacaoDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<VersaoDoTermo?> ObterVigenteSobTravaCompartilhadaAsync(CancellationToken cancellationToken = default)
    {
        await TravarAsync(TravaCompartilhada, cancellationToken).ConfigureAwait(false);

        return await ObterVigenteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(TermoDeResponsabilidade Termo, VersaoDoTermo? Vigente)> ObterParaPublicarAsync(CancellationToken cancellationToken = default)
    {
        await TravarAsync(TravaExclusiva, cancellationToken).ConfigureAwait(false);

        TermoDeResponsabilidade termo = await _context.TermosDeResponsabilidade
            .SingleAsync(t => t.Id == TermoDeResponsabilidade.RegistroUnicoId, cancellationToken)
            .ConfigureAwait(false);
        VersaoDoTermo? vigente = termo.VersaoVigenteId is { } id
            ? await ObterVersaoAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        return (termo, vigente);
    }

    public async Task<VersaoDoTermo?> ObterVigenteAsync(CancellationToken cancellationToken = default) =>
        await _context.VersoesDoTermo
            .AsNoTracking()
            .Where(v => _context.TermosDeResponsabilidade.Any(t => t.Id == TermoDeResponsabilidade.RegistroUnicoId && t.VersaoVigenteId == v.Id))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<VersaoDoTermo?> ObterVersaoAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.VersoesDoTermo.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id, cancellationToken).ConfigureAwait(false);

    // O número da versão é a ordem que a ADR promete; o Id (UUIDv7) só desempata, porque dois relógios ou o mesmo
    // milissegundo podem invertê-lo.
    public async Task<(IReadOnlyList<VersaoDoTermo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarVersoesAsync(
        string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default)
    {
        OrderedKeysetPage<VersaoDoTermo> page = await OrderedKeysetCursor
            .ApplyAsync(
                _context.VersoesDoTermo.AsNoTracking(),
                static b => b.Ascending(v => v.Numero).Ascending(v => v.Id),
                static v => v.Numero.ToString(CultureInfo.InvariantCulture),
                static (sortKey, id) => new { Numero = NumeroDaSortKey(sortKey), Id = id },
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, page.Previous, page.Next);
    }

    public async Task AdicionarVersaoAsync(VersaoDoTermo versao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(versao);
        await _context.VersoesDoTermo.AddAsync(versao, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarExcecaoDaInstalacaoAsync(TermoNaInstalacao excecao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        await _context.TermosNaInstalacao.AddAsync(excecao, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TermoNaInstalacao?> ObterExcecaoDaInstalacaoAsync(Guid instalacaoId, CancellationToken cancellationToken = default) =>
        await _context.TermosNaInstalacao.AsNoTracking().SingleOrDefaultAsync(t => t.InstalacaoId == instalacaoId, cancellationToken).ConfigureAwait(false);

    private static int NumeroDaSortKey(string sortKey) =>
        int.TryParse(sortKey, NumberStyles.None, CultureInfo.InvariantCulture, out int numero) ? numero : throw new CursorAnchorMismatchException();

    private Task<int> TravarAsync(string sql, CancellationToken cancellationToken) =>
        _context.Database.ExecuteSqlRawAsync(sql, [TermoDeResponsabilidade.RegistroUnicoId], cancellationToken);
}
