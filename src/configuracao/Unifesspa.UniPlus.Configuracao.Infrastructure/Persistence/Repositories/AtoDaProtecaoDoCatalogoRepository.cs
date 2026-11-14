namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class AtoDaProtecaoDoCatalogoRepository : IAtoDaProtecaoDoCatalogoRepository
{
    private const string EscapeDoLike = @"\";

    // A instituição conta o dia no fuso de Belém (sem horário de verão), e não em UTC.
    private static readonly TimeSpan FusoInstitucional = TimeZoneInfo.FindSystemTimeZoneById("America/Belem").BaseUtcOffset;

    private readonly UniPlusDbContext _dbContext;

    public AtoDaProtecaoDoCatalogoRepository(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public void Adicionar(AtoDaProtecaoDoCatalogo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        _dbContext.AtosDaProtecaoDoCatalogo.Add(ato);
    }

    public async Task<IReadOnlyList<AtoDaProtecaoDoCatalogo>> ListarDoItemAsync(
        CadastroDoCatalogo cadastro, Guid itemId, CancellationToken cancellationToken) =>
        await _dbContext.AtosDaProtecaoDoCatalogo
            .AsNoTracking()
            .Where(a => a.Cadastro == cadastro && a.ItemId == itemId)
            .OrderBy(a => a.Instante)
            .ThenBy(a => a.OrdemDeRegistro)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<AtoDaProtecaoDoCatalogo> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarDecisoesAsync(
        FiltroDasDecisoesDaProtecao filtro,
        string? afterSortKey,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        AtoDaProtecao[] atos = AtosConsideradosPor(filtro);
        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(filtro.Busca);
        IQueryable<DecisaoOrdenada> linhas = DecisoesDoRecorte(filtro, atos, termo);
        KeysetSort<DecisaoOrdenada> ordenacao = OrdenacaoDasDecisoesDaProtecao.Montar(RecorteDe(filtro, atos, termo));

        OrderedKeysetPage<DecisaoOrdenada> page = await OrderedKeysetCursor
            .ApplyAsync(linhas, ordenacao, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static l => l.Entidade)], page.Previous, page.Next);
    }

    private static AtoDaProtecao[] AtosConsideradosPor(FiltroDasDecisoesDaProtecao filtro) =>
        [.. (filtro.Atos.Count > 0 ? filtro.Atos : AtosDaProtecao.Decisoes).Distinct()];

    private IQueryable<DecisaoOrdenada> DecisoesDoRecorte(FiltroDasDecisoesDaProtecao filtro, AtoDaProtecao[] atos, string? termo)
    {
        IQueryable<AtoDaProtecaoDoCatalogo> query = _dbContext.AtosDaProtecaoDoCatalogo
            .AsNoTracking()
            .Where(a => atos.Contains(a.Ato));

        query = FiltrarPorCadastro(query, filtro);
        query = FiltrarPorPeriodo(query, filtro);
        query = FiltrarPorAutor(query, filtro);
        query = FiltrarPorBusca(query, termo);

        return query.Select(a => new DecisaoOrdenada { Id = a.Id, Instante = a.Instante, Entidade = a });
    }

    private static IQueryable<AtoDaProtecaoDoCatalogo> FiltrarPorCadastro(
        IQueryable<AtoDaProtecaoDoCatalogo> query, FiltroDasDecisoesDaProtecao filtro) =>
        filtro.Cadastro is { } cadastro ? query.Where(a => a.Cadastro == cadastro) : query;

    // A data máxima não tem dia seguinte: vale como sem limite superior.
    private static IQueryable<AtoDaProtecaoDoCatalogo> FiltrarPorPeriodo(
        IQueryable<AtoDaProtecaoDoCatalogo> query, FiltroDasDecisoesDaProtecao filtro)
    {
        if (filtro.De is { } de)
        {
            DateTimeOffset inicio = InicioDoDia(de);
            query = query.Where(a => a.Instante >= inicio);
        }

        if (filtro.Ate is { } ate && ate != DateOnly.MaxValue)
        {
            DateTimeOffset fim = InicioDoDia(ate.AddDays(1));
            query = query.Where(a => a.Instante < fim);
        }

        return query;
    }

    private static IQueryable<AtoDaProtecaoDoCatalogo> FiltrarPorAutor(
        IQueryable<AtoDaProtecaoDoCatalogo> query, FiltroDasDecisoesDaProtecao filtro) =>
        filtro.Autor is { } autor ? query.Where(a => a.AtorEmissor == autor.Emissor && a.AtorSub == autor.Sub) : query;

    // A busca compara a forma normalizada, sem acento e em minúsculas, do nome e do código do item.
    private static IQueryable<AtoDaProtecaoDoCatalogo> FiltrarPorBusca(IQueryable<AtoDaProtecaoDoCatalogo> query, string? termo)
    {
        if (termo is null)
        {
            return query;
        }

        string padrao = "%" + termo + "%";
        return query.Where(a =>
            EF.Functions.ILike(PgFunctions.NormalizeForComparison(a.ItemNome), padrao, EscapeDoLike)
            || EF.Functions.ILike(PgFunctions.NormalizeForComparison(a.ItemCodigo), padrao, EscapeDoLike));
    }

    // O provedor só grava e compara instante com deslocamento zero.
    private static DateTimeOffset InicioDoDia(DateOnly dia) =>
        new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), FusoInstitucional).ToUniversalTime();

    /// <summary>
    /// O que reduziu a coleção antes da paginação. Entra na assinatura do cursor para que a continuação de um
    /// recorte não retome dentro de outro: a âncora é uma posição num conjunto, e trocar o conjunto a torna sem sentido.
    /// </summary>
    private static List<string> RecorteDe(FiltroDasDecisoesDaProtecao filtro, AtoDaProtecao[] atos, string? termo) =>
    [
        string.Join(',', atos.Order().Select(static a => a.ToString())),
        filtro.Cadastro?.ToString() ?? string.Empty,
        filtro.De?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
        filtro.Ate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
        termo ?? string.Empty,
        filtro.Autor is { } autor ? $"{autor.Emissor}\u001f{autor.Sub}" : string.Empty,
    ];
}
