namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao;

internal sealed class CertameDivulgadoRepository(SelecaoDbContext context) : ICertameDivulgadoRepository
{
    /// <summary>
    /// Escape do <c>LIKE</c>. Explícito porque o padrão já chega com os curingas escapados por
    /// barra invertida, e o Postgres só a reconhece como escape quando ela é declarada.
    /// </summary>
    private const string EscapeDoLike = "\\";

    /// <summary>Quantas partes o recorte da vitrine acrescenta à assinatura da ordenação.</summary>
    private const int PartesDoRecorte = 5;

    /// <summary>Quantas colunas a ordem canônica por urgência tem, antes do identificador.</summary>
    private const int ColunasDaOrdemCanonica = 2;

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
            RecorteDaVitrine recorte,
            IReadOnlyList<SortField> ordenacao,
            TimeSpan limiarDosUltimosDias,
            string? afterSortKey,
            Guid? afterId,
            int limit,
            PaginationDirection direction,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorte);
        ArgumentNullException.ThrowIfNull(ordenacao);

        // O instante que segmenta é congelado na PRIMEIRA página e viaja na âncora: sem isso, um
        // prazo que vence no meio do percurso mudaria o item de segmento, e ele apareceria duas
        // vezes ou sumiria.
        DateTimeOffset instanteUtc =
            (InstanteDaAncora(afterSortKey, ordenacao.Count) ?? instanteSeForAPrimeiraPagina).ToUniversalTime();

        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(recorte.Busca);
        DateTimeOffset? revisao = await RevisaoDaColecaoAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<CertameNaVitrine> query = Recortar(instanteUtc, recorte, termo, limiarDosUltimosDias)
            .Select(c => new CertameNaVitrine
            {
                Id = c.Id,
                Encerrado = c.InscricoesAte < instanteUtc,
                NomeOrdenacao = EF.Property<string>(c, CertameDivulgadoConfiguration.NomeOrdenacaoPropriedade),
                InscricoesDe = c.InscricoesDe,
                InscricoesAte = c.InscricoesAte,
                DivulgadoEm = c.DivulgadoEm,
                Entidade = c,
            });

        OrderedKeysetPage<CertameNaVitrine> page = await OrderedKeysetCursor
            .ApplyAsync(
                query,
                OrdenacaoDaVitrine.Montar(ordenacao, AssinaturaDoRecorte(instanteUtc, recorte, termo, revisao)),
                afterSortKey,
                afterId,
                limit,
                direction,
                cancellationToken)
            .ConfigureAwait(false);

        return ([.. page.Items.Select(static linha => linha.Entidade)], instanteUtc, page.Previous, page.Next);
    }

    public async Task<ContadoresDaVitrine> ContarPorSituacaoAsync(
        DateTimeOffset instante,
        RecorteDaVitrine recorte,
        TimeSpan limiarDosUltimosDias,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorte);

        DateTimeOffset instanteUtc = instante.ToUniversalTime();
        DateTimeOffset limiar = instanteUtc + limiarDosUltimosDias;
        string? termo = NormalizacaoTextual.PrepararTermoDeBusca(recorte.Busca);

        // Os contadores alimentam o próprio filtro de situação, então correm sobre o recorte SEM
        // ela — aplicá-la deixaria todos zerados menos um. A busca e a modalidade, ao contrário,
        // entram: o número exibido promete quantos itens aquele filtro traz sobre o que está na tela.
        var contagem = await Recortar(instanteUtc, recorte with { Situacao = null }, termo, limiarDosUltimosDias)
            .GroupBy(static _ => 1)
            .Select(g => new
            {
                EmBreve = g.Count(c => c.InscricoesAte >= instanteUtc && c.InscricoesDe > instanteUtc),
                Abertas = g.Count(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= limiar),
                UltimosDias = g.Count(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
                Encerrados = g.Count(c => c.InscricoesAte < instanteUtc),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return contagem is null
            ? new ContadoresDaVitrine(0, 0, 0, 0)
            : new ContadoresDaVitrine(contagem.EmBreve, contagem.Abertas, contagem.UltimosDias, contagem.Encerrados);
    }

    /// <summary>
    /// Aplica o que reduz a coleção antes de ordenar: a situação da janela, a modalidade e a busca.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Um lugar só para os três porque a listagem e os contadores precisam correr sobre exatamente o
    /// mesmo conjunto — duas escritas do mesmo recorte é como o número ao lado do filtro passa a
    /// prometer uma lista diferente da que o filtro traz.
    /// </para>
    /// <para>
    /// Os quatro recortes de situação particionam o conjunto divulgado: cada certame cai em
    /// exatamente um. São o predicado traduzível da regra que <see cref="SituacaoDaVitrine.Classificar"/>
    /// enuncia — o banco não pode chamá-la. A conferência de que as duas concordam é obrigação de
    /// teste, contra Postgres real: divergirem em silêncio é o defeito que este recorte pode ter.
    /// </para>
    /// </remarks>
    private IQueryable<CertameDivulgado> Recortar(
        DateTimeOffset instanteUtc,
        RecorteDaVitrine recorte,
        string? termo,
        TimeSpan limiarDosUltimosDias)
    {
        DateTimeOffset limiar = instanteUtc + limiarDosUltimosDias;

        IQueryable<CertameDivulgado> query = _context.CertamesDivulgados.AsNoTracking();

        query = recorte.Situacao switch
        {
            SituacaoDoCertame.EmBreve => query.Where(c => c.InscricoesAte >= instanteUtc && c.InscricoesDe > instanteUtc),
            SituacaoDoCertame.InscricoesAbertas => query.Where(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= limiar),
            SituacaoDoCertame.UltimosDias => query.Where(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
            SituacaoDoCertame.Encerradas => query.Where(c => c.InscricoesAte < instanteUtc),
            _ => query,
        };

        if (recorte.Modalidade is { Length: > 0 } modalidade)
        {
            // Pertinência ao arranjo, servida pelo índice GIN. Comparar o código como veio é o
            // correto aqui: modalidade é vocabulário fechado, não texto que alguém digita.
            query = query.Where(c => c.ModalidadesOfertadas.Contains(modalidade));
        }

        if (termo is not null)
        {
            // O título compara contra a mesma coluna normalizada que ordena, então acento e caixa já
            // não participam dele. O número do edital é normalizado na consulta, pelo banco.
            string padrao = "%" + termo + "%";
            query = query.Where(c =>
                EF.Functions.ILike(
                    EF.Property<string>(c, CertameDivulgadoConfiguration.NomeOrdenacaoPropriedade), padrao, EscapeDoLike)
                || (c.Numero != null
                    && EF.Functions.ILike(PgFunctions.NormalizeForComparison(c.Numero), padrao, EscapeDoLike)));
        }

        return query;
    }

    /// <summary>
    /// Identifica o RECORTE sobre o qual a travessia corre, e o instante em que ela foi congelada.
    /// Viaja na chave da âncora para que um cursor só continue a consulta que o emitiu.
    /// </summary>
    /// <remarks>
    /// A âncora é uma posição <b>dentro de um conjunto</b>. Sem esta assinatura, o cursor emitido na
    /// lista dos encerrados seria aceito de volta com outra situação, ou sob outra busca, e o seek
    /// partiria de um valor que não existe naquele conjunto: a página voltaria vazia e sem
    /// continuação, indistinguível de fim de coleção.
    /// </remarks>
    private static IReadOnlyList<string> AssinaturaDoRecorte(
        DateTimeOffset instanteUtc,
        RecorteDaVitrine recorte,
        string? termo,
        DateTimeOffset? revisao) =>
    [
        Instante(instanteUtc),
        recorte.Situacao?.ToString() ?? string.Empty,
        recorte.Modalidade ?? string.Empty,
        termo ?? string.Empty,
        revisao is { } r ? Instante(r) : string.Empty,
    ];

    /// <summary>
    /// Instante da última mudança na coleção — nula quando não há certame divulgado.
    /// </summary>
    /// <remarks>
    /// Entra na assinatura do cursor porque a âncora guarda uma POSIÇÃO, e o prazo que a define
    /// muda: uma retificação no meio do percurso reposiciona um certame em relação à âncora, e quem
    /// a cruza num sentido aparece duas vezes, quem cruza no outro desaparece. Com a revisão na
    /// assinatura, a continuação sob coleção diferente é recusada e o cliente recomeça — que é o
    /// que a ADR-0131 exige em vez de uma lista silenciosamente inconsistente.
    /// </remarks>
    private Task<DateTimeOffset?> RevisaoDaColecaoAsync(CancellationToken cancellationToken) =>
        _context.CertamesDivulgados
            .AsNoTracking()
            .MaxAsync(c => (DateTimeOffset?)c.DivulgadoEm, cancellationToken);

    private static string Instante(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Lê o instante congelado de dentro da chave que veio no cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chave é o serializado de <c>[assinatura, ...valores das colunas]</c>, e a assinatura é, por
    /// sua vez, o serializado de <c>[campo, sentido, ...] + recorte</c>. O instante é a primeira
    /// parte do recorte, e chegar até ele é desembrulhar duas vezes — as duas com a contagem exata,
    /// que é o que torna a leitura estrita.
    /// </para>
    /// <para>
    /// Devolve nulo quando a chave não tem essa forma, e a consulta parte do instante de agora.
    /// Isso NÃO abre porta para paginar do lugar errado: a âncora é reconstruída em seguida pela
    /// própria ordenação, que confere a assinatura inteira e recusa o que não bater.
    /// </para>
    /// </remarks>
    private static DateTimeOffset? InstanteDaAncora(string? sortKey, int colunasPedidas)
    {
        int colunas = colunasPedidas == 0 ? ColunasDaOrdemCanonica : colunasPedidas;

        if (!CompositeSortKey.TryDeserialize(sortKey, colunas + 1, out IReadOnlyList<string> partes)
            || !CompositeSortKey.TryDeserialize(partes[0], (colunas * 2) + PartesDoRecorte, out IReadOnlyList<string> assinatura)
            || !TentarLerInstante(assinatura[colunas * 2], out DateTimeOffset instante))
        {
            return null;
        }

        return instante;
    }

    /// <summary>
    /// Lê um instante da âncora assumindo UTC na ausência de designador de fuso.
    /// </summary>
    /// <remarks>
    /// Sem <see cref="DateTimeStyles.AssumeUniversal"/>, um texto sem o <c>Z</c> receberia o fuso
    /// LOCAL do processo que lê, e a mesma âncora retomaria de posições diferentes conforme a
    /// máquina que serve a requisição.
    /// </remarks>
    private static bool TentarLerInstante(string texto, out DateTimeOffset valor) =>
        DateTimeOffset.TryParse(
            texto,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out valor);
}
