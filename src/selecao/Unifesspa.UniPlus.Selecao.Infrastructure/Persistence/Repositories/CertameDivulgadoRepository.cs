namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

internal sealed class CertameDivulgadoRepository(SelecaoDbContext context) : ICertameDivulgadoRepository
{
    private readonly SelecaoDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public Task<CertameDivulgado?> ObterParaMutacaoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default) =>
        _context.CertamesDivulgados.FirstOrDefaultAsync(c => c.Id == processoSeletivoId, cancellationToken);

    public Task<CertameDivulgado?> ObterParaLeituraAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default) =>
        _context.CertamesDivulgados.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == processoSeletivoId, cancellationToken);

    public async Task AdicionarAsync(CertameDivulgado divulgado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(divulgado);
        await _context.CertamesDivulgados.AddAsync(divulgado, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        ListarVitrineAsync(
            DateTimeOffset instanteSeForAPrimeiraPagina,
            SituacaoDoCertame situacao,
            TimeSpan limiarDosUltimosDias,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken = default)
    {
        // O instante que segmenta é congelado na PRIMEIRA página e viaja na âncora: sem isso, um
        // prazo que vence no meio do percurso mudaria o item de segmento, e ele apareceria duas
        // vezes ou sumiria.
        DateTimeOffset instanteUtc = (InstanteDaAncora(afterSortKey) ?? instanteSeForAPrimeiraPagina).ToUniversalTime();
        DateTimeOffset limiar = instanteUtc + limiarDosUltimosDias;

        IQueryable<CertameDivulgado> query = _context.CertamesDivulgados.AsNoTracking();

        // Os três recortes particionam o conjunto divulgado: cada certame cai em exatamente um, e é
        // isso que faz cada contador ter um filtro que o serve.
        query = situacao switch
        {
            SituacaoDoCertame.InscricoesAbertas => query.Where(c => c.InscricoesAte >= limiar),
            SituacaoDoCertame.UltimosDias => query.Where(c => c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
            SituacaoDoCertame.Encerradas => query.Where(c => c.InscricoesAte < instanteUtc),
            _ => query,
        };

        // A ordem por urgência é uma rotação da ordem de prazo no ponto do instante congelado.
        OrderedKeysetPage<CertameDivulgado> page = await OrderedKeysetCursor
            .ApplyAsync(
                query,
                b => b
                    .Ascending(c => c.InscricoesAte < instanteUtc)
                    .Ascending(c => c.InscricoesAte)
                    .Ascending(c => c.Id),
                c => SortKeyDaVitrine(c, instanteUtc),
                AncoraDaVitrine,
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return (page.Items, instanteUtc, page.Previous, page.Next);
    }

    public async Task<ContadoresDaVitrine> ContarPorSituacaoAsync(
        DateTimeOffset instante,
        TimeSpan limiarDosUltimosDias,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        DateTimeOffset limiar = instanteUtc + limiarDosUltimosDias;

        // Agrupamento constante para o provider emitir UMA consulta com as três contagens
        // condicionais: são números que a tela exibe lado a lado, e resolvê-los separadamente
        // abriria janela para discordarem entre si.
        var contagem = await _context.CertamesDivulgados
            .AsNoTracking()
            .GroupBy(static _ => 1)
            .Select(g => new
            {
                Abertas = g.Count(c => c.InscricoesAte >= limiar),
                UltimosDias = g.Count(c => c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
                Encerrados = g.Count(c => c.InscricoesAte < instanteUtc),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return contagem is null
            ? new ContadoresDaVitrine(0, 0, 0)
            : new ContadoresDaVitrine(contagem.Abertas, contagem.UltimosDias, contagem.Encerrados);
    }

    /// <summary>
    /// Chave de ordenação da âncora: o instante congelado e o prazo, nessa ordem. O segmento não
    /// entra — ele deriva dos dois, e guardá-lo seria uma segunda cópia do mesmo fato.
    /// </summary>
    private static string SortKeyDaVitrine(CertameDivulgado certame, DateTimeOffset instanteUtc) =>
        CompositeSortKey.Serialize(Instante(instanteUtc), Instante(certame.InscricoesAte));

    private static object AncoraDaVitrine(string sortKey, Guid id)
    {
        if (!CompositeSortKey.TryDeserialize(sortKey, 2, out IReadOnlyList<string> partes)
            || !DateTimeOffset.TryParse(
                partes[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTimeOffset prazo))
        {
            throw new CursorAnchorMismatchException("Âncora da vitrine fora da forma esperada.");
        }

        // Os membros casam a cadeia de propriedades que a consulta ordena — é por ela que o motor
        // de seek resolve cada coluna no objeto de referência.
        return new { InscricoesAte = prazo, Id = id };
    }

    private static string Instante(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private static DateTimeOffset? InstanteDaAncora(string? sortKey) =>
        CompositeSortKey.TryDeserialize(sortKey, 2, out IReadOnlyList<string> partes)
            && DateTimeOffset.TryParse(
                partes[0], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTimeOffset instante)
            ? instante
            : null;
}
