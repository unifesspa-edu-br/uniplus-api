namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao;

internal sealed class CertameDivulgadoRepository(UniPlusDbContext context) : ICertameDivulgadoRepository
{
    private const int PartesDaConsultaNaAssinatura = 5;

    /// <summary>Quantas colunas a ordem canônica por urgência tem, antes do identificador.</summary>
    private const int ColunasDaOrdemCanonica = 2;

    private readonly UniPlusDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public Task<CertameDivulgado?> ObterParaMutacaoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default) =>
        _context.CertamesDivulgados.FirstOrDefaultAsync(c => c.Id == processoSeletivoId, cancellationToken);

    public Task<CertameDivulgado?> ObterParaLeituraAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default) =>
        _context.CertamesDivulgados.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == processoSeletivoId, cancellationToken);

    public Task<CertameDivulgado?> ObterParaLeituraPorIdentificadorAsync(
        string identificadorLegivel,
        CancellationToken cancellationToken = default) =>
        _context.CertamesDivulgados.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentificadorLegivel == identificadorLegivel, cancellationToken);

    public async Task AdicionarAsync(CertameDivulgado divulgado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(divulgado);
        await _context.CertamesDivulgados.AddAsync(divulgado, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>O instante é preservado no cursor e as leituras compartilham o mesmo instantâneo.</remarks>
    public async Task<PaginaDaVitrine> ListarVitrineAsync(
        ConsultaDosCertamesPublicados consulta, string? afterSortKey, Guid? afterId, int limit,
        PaginationDirection direction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        DateTimeOffset instanteUtc = (InstanteDaAncora(afterSortKey, consulta.Ordenacao.Count)
            ?? consulta.InstanteSeForAPrimeiraPagina).ToUniversalTime();
        IDbContextTransaction instantaneo = await _context.Database
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable descarte = instantaneo.ConfigureAwait(false);
        PaginaDaVitrine pagina = await LerPaginaAsync(consulta, instanteUtc, afterSortKey, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);
        await instantaneo.CommitAsync(cancellationToken).ConfigureAwait(false);
        return pagina;
    }

    private async Task<PaginaDaVitrine> LerPaginaAsync(
        ConsultaDosCertamesPublicados consulta, DateTimeOffset instanteUtc, string? afterSortKey,
        Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken)
    {
        string revisao = await RevisaoDosCertamesAsync(consulta, instanteUtc, cancellationToken).ConfigureAwait(false);
        OrderedKeysetPage<CertameNaVitrine> page = await OrderedKeysetCursor.ApplyAsync(
            LinhasParaOrdenar(consulta, instanteUtc),
            OrdenacaoDaVitrine.Montar(consulta.Ordenacao, AssinaturaDaConsulta(consulta.Filtro, instanteUtc, revisao)),
            afterSortKey, afterId, limit, direction, cancellationToken).ConfigureAwait(false);
        ContadoresDaVitrine? contadores = await ContadoresSePedidosAsync(consulta, instanteUtc, cancellationToken).ConfigureAwait(false);
        return new PaginaDaVitrine([.. page.Items.Select(static linha => linha.Entidade)],
            instanteUtc, page.Previous, page.Next, contadores, revisao);
    }

    private IQueryable<CertameDivulgado> CertamesServidos(string versao) =>
        _context.CertamesDivulgados.AsNoTracking().Where(c => c.VersaoProjecao == versao);

    private IQueryable<CertameDivulgado> CertamesDaConsulta(ConsultaDosCertamesPublicados consulta, DateTimeOffset instanteUtc) =>
        CertamesServidos(consulta.VersaoDaProjecaoServida).Filtrados(consulta.Filtro, instanteUtc);

    private IQueryable<CertameNaVitrine> LinhasParaOrdenar(ConsultaDosCertamesPublicados consulta, DateTimeOffset instanteUtc) =>
        CertamesDaConsulta(consulta, instanteUtc).Select(c => new CertameNaVitrine
        {
            Id = c.Id,
            Encerrado = c.InscricoesAte < instanteUtc,
            NomeOrdenacao = EF.Property<string>(c, CertameDivulgadoConfiguration.NomeOrdenacaoPropriedade),
            InscricoesDe = c.InscricoesDe,
            InscricoesAte = c.InscricoesAte,
            DivulgadoEm = c.DivulgadoEm,
            Entidade = c,
        });

    /// <remarks>A contagem exclui a situação para informar o resultado de cada opção do filtro.</remarks>
    private async Task<ContadoresDaVitrine?> ContadoresSePedidosAsync(
        ConsultaDosCertamesPublicados consulta, DateTimeOffset instanteUtc, CancellationToken cancellationToken) =>
        consulta.IncluirContadores
            ? await ContagemPorSituacao(CertamesServidos(consulta.VersaoDaProjecaoServida)
                .FiltradosSemSituacao(consulta.Filtro), instanteUtc, cancellationToken).ConfigureAwait(false)
            : null;

    private static async Task<ContadoresDaVitrine> ContagemPorSituacao(
        IQueryable<CertameDivulgado> certames, DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        DateTimeOffset limiar = instanteUtc + SituacaoDaVitrine.LimiarDosUltimosDias;
        return await certames.GroupBy(static _ => 1).Select(g => new ContadoresDaVitrine(
            g.Count(c => c.InscricoesAte >= instanteUtc && c.InscricoesDe > instanteUtc),
            g.Count(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= limiar),
            g.Count(c => c.InscricoesDe <= instanteUtc && c.InscricoesAte >= instanteUtc && c.InscricoesAte < limiar),
            g.Count(c => c.InscricoesAte < instanteUtc))).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> RevisaoDosCertamesAsync(
        ConsultaDosCertamesPublicados consulta, DateTimeOffset instanteUtc, CancellationToken cancellationToken)
    {
        var linhas = await CertamesDaConsulta(consulta, instanteUtc).OrderBy(static c => c.Id)
            .Select(static c => new { c.Id, c.AtoCriadorId, c.InscricoesDe, c.InscricoesAte })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        string texto = string.Concat(linhas.Select(c => ParteDaRevisao(c.Id, c.AtoCriadorId,
            SituacaoDaVitrine.Classificar(c.InscricoesDe, c.InscricoesAte, instanteUtc, SituacaoDaVitrine.LimiarDosUltimosDias))));
        return ResumoDaRevisao(texto);
    }

    private static string ParteDaRevisao(Guid id, Guid atoCriadorId, SituacaoDoCertame situacao) =>
        $"{id:N}:{atoCriadorId:N}:{(int)situacao};";

    private static string ResumoDaRevisao(string texto) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)).AsSpan(0, 16));

    private static IReadOnlyList<string> AssinaturaDaConsulta(FiltroDosCertamesPublicados filtro, DateTimeOffset instanteUtc, string revisao) =>
        [Instante(instanteUtc), filtro.Situacao?.ToString() ?? string.Empty, filtro.Modalidade ?? string.Empty,
            NormalizacaoTextual.PrepararTermoDeBusca(filtro.Busca) ?? string.Empty, revisao];

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
            || !CompositeSortKey.TryDeserialize(partes[0], (colunas * 2) + PartesDaConsultaNaAssinatura, out IReadOnlyList<string> assinatura)
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
