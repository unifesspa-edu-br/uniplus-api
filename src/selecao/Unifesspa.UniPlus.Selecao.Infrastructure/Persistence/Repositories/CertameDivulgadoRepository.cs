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
                c => SortKeyDaVitrine(c, instanteUtc, situacao),
                (sortKey, id) => AncoraDaVitrine(sortKey, id, situacao),
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
    /// Identifica o RECORTE sobre o qual a travessia corre. Viaja na chave da âncora para que um
    /// cursor só continue a consulta que o emitiu.
    /// </summary>
    /// <remarks>
    /// A âncora é uma posição <b>dentro de um conjunto</b>. Sem esta assinatura, o cursor emitido
    /// na lista dos encerrados seria aceito de volta com <c>situacao=inscricoesAbertas</c>, e o
    /// seek partiria de um prazo que não existe naquele conjunto: a página voltaria vazia e sem
    /// continuação, indistinguível de fim de coleção. É a mesma proteção que
    /// <c>KeysetSort.Signature</c> dá às listagens que declaram a ordenação pelo catálogo.
    /// </remarks>
    private static string AssinaturaDaVitrine(SituacaoDoCertame situacao) =>
        string.Create(CultureInfo.InvariantCulture, $"vitrine-certames:{situacao}");

    /// <summary>
    /// Chave de ordenação da âncora: a assinatura do recorte, o instante congelado e o prazo,
    /// nessa ordem. O segmento não entra — ele deriva dos dois últimos, e guardá-lo seria uma
    /// segunda cópia do mesmo fato.
    /// </summary>
    private static string SortKeyDaVitrine(CertameDivulgado certame, DateTimeOffset instanteUtc, SituacaoDoCertame situacao) =>
        CompositeSortKey.Serialize(
            AssinaturaDaVitrine(situacao), Instante(instanteUtc), Instante(certame.InscricoesAte));

    private static object AncoraDaVitrine(string sortKey, Guid id, SituacaoDoCertame situacao)
    {
        if (!CompositeSortKey.TryDeserialize(sortKey, 3, out IReadOnlyList<string> partes)
            || !string.Equals(partes[0], AssinaturaDaVitrine(situacao), StringComparison.Ordinal)
            || !TentarLerInstante(partes[2], out DateTimeOffset prazo))
        {
            throw new CursorAnchorMismatchException("Âncora da vitrine fora da forma esperada.");
        }

        // Os membros casam a cadeia de propriedades que a consulta ordena — é por ela que o motor
        // de seek resolve cada coluna no objeto de referência.
        return new { InscricoesAte = prazo, Id = id };
    }

    private static string Instante(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Lê um instante da âncora assumindo UTC na ausência de designador de fuso.
    /// </summary>
    /// <remarks>
    /// Sem <see cref="DateTimeStyles.AssumeUniversal"/>, um texto sem o <c>Z</c> receberia o fuso
    /// LOCAL do processo que lê, e a mesma âncora retomaria de posições diferentes conforme a
    /// máquina que serve a requisição. É a mesma disciplina que a projeção do certame aplica ao
    /// instante congelado.
    /// </remarks>
    private static bool TentarLerInstante(string texto, out DateTimeOffset valor) =>
        DateTimeOffset.TryParse(
            texto,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out valor);

    private static DateTimeOffset? InstanteDaAncora(string? sortKey) =>
        CompositeSortKey.TryDeserialize(sortKey, 3, out IReadOnlyList<string> partes)
            && TentarLerInstante(partes[1], out DateTimeOffset instante)
            ? instante
            : null;
}
