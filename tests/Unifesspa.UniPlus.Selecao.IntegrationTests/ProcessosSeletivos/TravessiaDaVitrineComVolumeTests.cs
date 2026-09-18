namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Xunit;

/// <summary>
/// <b>Percorrer a vitrine inteira, página a página, sem repetir nem omitir certame.</b>
/// </summary>
/// <remarks>
/// <para>
/// Volume importa aqui por um motivo específico, e não por realismo: o defeito do seek keyset é o
/// da <b>fronteira entre páginas</b>. Com três linhas e uma página, nenhuma fronteira é exercitada;
/// o erro de um lado da desigualdade, o desempate que falta, a coluna que a âncora não carrega —
/// todos passam despercebidos. Com muitas páginas e empates deliberados em toda coluna de
/// ordenação, cada fronteira vira uma chance de o defeito aparecer.
/// </para>
/// <para>
/// Os empates são construídos de propósito: vários certames com o mesmo prazo, vários com o mesmo
/// título, vários com o mesmo instante de divulgação. Sem eles, a ordenação seria acidentalmente
/// única e o desempate por identificador — que é o que garante ordem total — nunca seria testado.
/// </para>
/// </remarks>
public sealed class TravessiaDaVitrineComVolumeTests : IClassFixture<ProcessoSeletivoDbFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limiar = TimeSpan.FromDays(7);

    /// <summary>Quantos certames a vitrine tem nesta suíte.</summary>
    private const int Volume = 250;

    /// <summary>Tamanho da página. Não divide o volume: a última página parcial é um caso próprio.</summary>
    private const int TamanhoDaPagina = 24;

    private readonly ProcessoSeletivoDbFixture _fixture;

    /// <summary>
    /// Teto de páginas de uma travessia. Uma que não termina é defeito, e sem o teto ela vira um
    /// teste que roda para sempre em vez de falhar.
    /// </summary>
    private static readonly int TetoDePaginas = (Volume / TamanhoDaPagina) + 5;

    private readonly List<Guid> _semeados = [];

    public TravessiaDaVitrineComVolumeTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE selecao.certames_divulgados");

        List<CertameDivulgado> linhas = [];
        for (int i = 0; i < Volume; i++)
        {
            // Empates deliberados em toda coluna de ordenação, e os dois segmentos da rotação.
            int diasAtePrazo = ((i % 25) * 4) - 50;
            CertameDivulgado linha = Divulgado(
                nome: string.Create(CultureInfo.InvariantCulture, $"Certame {(char)('A' + (i % 12))}"),
                numero: string.Create(CultureInfo.InvariantCulture, $"{i:D3}/2026"),
                inscricoesAte: Agora.AddDays(diasAtePrazo));

            linhas.Add(linha);
            _semeados.Add(linha.Id);
        }

        context.CertamesDivulgados.AddRange(linhas);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "A travessia para a frente devolve cada certame exatamente uma vez")]
    [InlineData(null, null)]
    [InlineData(CamposOrdenacaoDaVitrine.Nome, "asc")]
    [InlineData(CamposOrdenacaoDaVitrine.Nome, "desc")]
    [InlineData(CamposOrdenacaoDaVitrine.DivulgadoEm, "asc")]
    [InlineData(CamposOrdenacaoDaVitrine.InscricoesDe, "desc")]
    public async Task Travessia_QuandoPercorreParaAFrente_DeveCobrirOConjuntoUmaVez(string? campo, string? sentido)
    {
        IReadOnlyList<Guid> percorridos = await PercorrerAsync(Ordenacao(campo, sentido), PaginationDirection.Next);

        percorridos.Should().OnlyHaveUniqueItems("um certame repetido entre páginas é o seek partindo do lugar errado");
        percorridos.Should().BeEquivalentTo(_semeados, "nenhum certame pode ficar de fora da travessia");
    }

    [Fact(DisplayName = "A travessia para trás cobre o mesmo conjunto que a de ida")]
    public async Task Travessia_QuandoPercorreParaTras_DeveCobrirOMesmoConjunto()
    {
        // O sentido reverso tem seek e ordenação próprios: erro só nele dá ida correta e volta
        // com buraco.
        IReadOnlyList<Guid> voltando = await PercorrerAsync([], PaginationDirection.Prev);

        voltando.Should().OnlyHaveUniqueItems();
        voltando.Should().BeEquivalentTo(_semeados);
    }

    [Fact(DisplayName = "A ordem canônica por urgência vale para a coleção inteira, não por página")]
    public async Task Travessia_QuandoNaoPedeOrdenacao_DeveManterAUrgenciaNaColecaoInteira()
    {
        // Ordenar por página, e não pela coleção, só se revela olhando a sequência inteira.
        IReadOnlyList<(bool Encerrado, DateTimeOffset Prazo)> chaves = await PercorrerChavesAsync([]);

        chaves.Should().BeInAscendingOrder(
            static c => c.Encerrado,
            "os que ainda não encerraram vêm antes, e o segmento não se alterna no meio da travessia");

        chaves.Where(static c => !c.Encerrado).Select(static c => c.Prazo)
            .Should().BeInAscendingOrder("dentro dos abertos, o prazo mais próximo vem primeiro");

        chaves.Where(static c => c.Encerrado).Select(static c => c.Prazo)
            .Should().BeInAscendingOrder("dentro dos encerrados, a ordem por prazo também vale");
    }

    [Fact(DisplayName = "A ordenação decrescente pedida vale para a coleção inteira")]
    public async Task Travessia_QuandoPedeOrdenacaoDecrescente_DeveValerNaColecaoInteira()
    {
        IReadOnlyList<string> nomes = await PercorrerNomesAsync(
            [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Descending)]);

        nomes.Should().HaveCount(Volume);
        nomes.Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    private static IReadOnlyList<SortField> Ordenacao(string? campo, string? sentido) =>
        campo is null
            ? []
            : [new SortField(campo, sentido == "desc" ? SortDirection.Descending : SortDirection.Ascending)];

    private async Task<IReadOnlyList<Guid>> PercorrerAsync(
        IReadOnlyList<SortField> ordenacao,
        PaginationDirection direcao) =>
        [.. (direcao == PaginationDirection.Prev
            ? await PercorrerDeVoltaAsync(ordenacao)
            : await PercorrerItensAsync(ordenacao)).Select(static c => c.Id)];

    private async Task<IReadOnlyList<(bool Encerrado, DateTimeOffset Prazo)>> PercorrerChavesAsync(
        IReadOnlyList<SortField> ordenacao) =>
        [.. (await PercorrerItensAsync(ordenacao))
            .Select(static c => (c.InscricoesAte < Agora, c.InscricoesAte))];

    private async Task<IReadOnlyList<string>> PercorrerNomesAsync(IReadOnlyList<SortField> ordenacao) =>
        [.. (await PercorrerItensAsync(ordenacao)).Select(static c => c.Nome)];

    /// <summary>
    /// Percorre a vitrine para a frente seguindo as âncoras que cada página emite, exatamente como o
    /// cliente faria pelo header de navegação.
    /// </summary>
    private async Task<IReadOnlyList<CertameDivulgado>> PercorrerItensAsync(IReadOnlyList<SortField> ordenacao)
    {
        List<CertameDivulgado> acumulados = [];
        (string SortKey, Guid Id)? cursor = null;

        for (int tentativa = 0; tentativa < TetoDePaginas; tentativa++)
        {
            (IReadOnlyList<CertameDivulgado> itens, _, (string SortKey, Guid Id)? proximo) =
                await PaginaAsync(ordenacao, cursor, PaginationDirection.Next);

            if (itens.Count == 0)
            {
                return acumulados;
            }

            acumulados.AddRange(itens);

            if (proximo is null)
            {
                return acumulados;
            }

            cursor = proximo;
        }

        throw new InvalidOperationException(
            $"A travessia não terminou em {TetoDePaginas} páginas — o cursor não converge.");
    }

    /// <summary>
    /// Percorre a vitrine de trás para a frente, como o cliente que chegou ao fim e volta.
    /// </summary>
    /// <remarks>
    /// A volta parte do <b>Anterior</b> que a última página emitiu, e não da âncora que a buscou: é
    /// a âncora que o header de navegação carrega em <c>rel="prev"</c>, e é a posição do PRIMEIRO
    /// item da página corrente. A última página entra no conjunto por já estar em mãos — é onde o
    /// cliente está quando decide voltar.
    /// </remarks>
    private async Task<IReadOnlyList<CertameDivulgado>> PercorrerDeVoltaAsync(IReadOnlyList<SortField> ordenacao)
    {
        (IReadOnlyList<CertameDivulgado> ultimaPagina, (string SortKey, Guid Id)? anteriorDaUltima) =
            await UltimaPaginaAsync(ordenacao);

        List<CertameDivulgado> acumulados = [.. ultimaPagina];
        (string SortKey, Guid Id)? cursor = anteriorDaUltima;

        for (int tentativa = 0; tentativa < TetoDePaginas; tentativa++)
        {
            if (cursor is null)
            {
                return acumulados;
            }

            (IReadOnlyList<CertameDivulgado> itens, (string SortKey, Guid Id)? anterior, _) =
                await PaginaAsync(ordenacao, cursor, PaginationDirection.Prev);

            if (itens.Count == 0)
            {
                return acumulados;
            }

            acumulados.InsertRange(0, itens);
            cursor = anterior;
        }

        throw new InvalidOperationException(
            $"A volta não terminou em {TetoDePaginas} páginas — o cursor não converge.");
    }

    /// <summary>A última página da coleção e a âncora que leva dela para a anterior.</summary>
    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Anterior)> UltimaPaginaAsync(
        IReadOnlyList<SortField> ordenacao)
    {
        (string SortKey, Guid Id)? cursor = null;

        for (int tentativa = 0; tentativa < TetoDePaginas; tentativa++)
        {
            (IReadOnlyList<CertameDivulgado> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
                await PaginaAsync(ordenacao, cursor, PaginationDirection.Next);

            if (proximo is null)
            {
                return (itens, anterior);
            }

            cursor = proximo;
        }

        throw new InvalidOperationException("Não foi possível alcançar a última página.");
    }

    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        PaginaAsync(
            IReadOnlyList<SortField> ordenacao,
            (string SortKey, Guid Id)? cursor,
            PaginationDirection direcao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        (IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo) pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), ordenacao, Limiar, cursor?.SortKey, cursor?.Id,
                TamanhoDaPagina, direcao, CancellationToken.None);

        return (pagina.Itens, pagina.Anterior, pagina.Proximo);
    }

    private static CertameDivulgado Divulgado(string nome, string numero, DateTimeOffset inscricoesAte) =>
        CertameDivulgado.Criar(
            Guid.CreateVersion7(),
            numeroVersao: 1,
            Guid.CreateVersion7(),
            new string('a', 64),
            versaoProjecao: "1",
            new FacetasDoCertameDivulgado(nome, numero, ["AC"], Agora.AddDays(-60), inscricoesAte),
            """{"nome":"documento"}""",
            Agora);
}
