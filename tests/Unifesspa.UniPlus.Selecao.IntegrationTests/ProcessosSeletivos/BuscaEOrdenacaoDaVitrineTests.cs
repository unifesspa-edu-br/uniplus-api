namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Xunit;

/// <summary>
/// <b>Busca, recorte por modalidade e ordenação escolhida pela consulta, contra Postgres real.</b>
/// </summary>
/// <remarks>
/// Os três só existem no banco: a busca compara contra uma coluna gerada, o recorte por modalidade
/// depende de um operador de arranjo, e a ordenação é o SQL que o motor de seek emite. Em memória,
/// nenhum deles estaria sendo exercitado — e o defeito clássico de cada um (acento que não casa,
/// ordem por ponto de código, cursor que atravessa recorte) só aparece contra o banco.
/// </remarks>
public sealed class BuscaEOrdenacaoDaVitrineTests : IClassFixture<ProcessoSeletivoDbFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limiar = TimeSpan.FromDays(7);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public BuscaEOrdenacaoDaVitrineTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE selecao.certames_divulgados");

        context.CertamesDivulgados.AddRange(
            Divulgado("Vestibular de Música", "003/2026", ["AC", "LI_EP"], Agora.AddDays(30)),
            Divulgado("Ingresso à Pós-Graduação", "001/2026", ["AC"], Agora.AddDays(10)),
            Divulgado("SISU 2026.1", "002/2026", ["LI_PPI"], Agora.AddDays(20)));

        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "A busca ignora acento e caixa, dos dois lados")]
    [InlineData("musica")]
    [InlineData("MÚSICA")]
    [InlineData("Música")]
    [InlineData("músic")]
    public async Task ListarVitrine_QuandoTermoVariaEmAcentoECaixa_DeveEncontrarOMesmoCertame(string termo)
    {
        // O texto guardado tem acento e o pesquisado pode não ter, ou o contrário. Normalizar só um
        // lado é o defeito que faz a busca não achar o que existe — e ele não aparece em memória,
        // onde a comparação é a mesma dos dois lados.
        IReadOnlyList<string> nomes = await ListarAsync(new RecorteDaVitrine(Busca: termo));

        nomes.Should().ContainSingle().Which.Should().Be("Vestibular de Música");
    }

    [Fact(DisplayName = "A busca alcança o número do edital, não só o título")]
    public async Task ListarVitrine_QuandoTermoEONumeroDoEdital_DeveEncontrarOCertame()
    {
        IReadOnlyList<string> nomes = await ListarAsync(new RecorteDaVitrine(Busca: "002/2026"));

        nomes.Should().ContainSingle().Which.Should().Be("SISU 2026.1");
    }

    [Fact(DisplayName = "Curinga digitado é procurado como texto, não como curinga")]
    public async Task ListarVitrine_QuandoTermoTemCuringaDoLike_DeveProcuraLoComoTexto()
    {
        // Sem escapar, '%' casaria com tudo e a busca devolveria a vitrine inteira — dando ao
        // usuário a impressão de que o termo dele existe em todo certame.
        IReadOnlyList<string> nomes = await ListarAsync(new RecorteDaVitrine(Busca: "%"));

        nomes.Should().BeEmpty();
    }

    [Fact(DisplayName = "O recorte por modalidade traz só quem a oferta")]
    public async Task ListarVitrine_QuandoRecortaPorModalidade_DeveTrazerSoQuemAOferta()
    {
        IReadOnlyList<string> nomes = await ListarAsync(new RecorteDaVitrine(Modalidade: "AC"));

        nomes.Should().BeEquivalentTo(["Vestibular de Música", "Ingresso à Pós-Graduação"]);
    }

    [Fact(DisplayName = "A ordenação por título é alfabética real, não por ponto de código")]
    public async Task Ordenacao_PorNome_EAlfabeticaReal()
    {
        // Pelo ponto de código, "Ingresso à..." viria depois de "SISU" e de "Vestibular" — o acento
        // do 'à' está acima de toda letra ASCII. É o que a coluna normalizada existe para corrigir.
        IReadOnlyList<string> nomes = await ListarAsync(
            new RecorteDaVitrine(), [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Ascending)]);

        nomes.Should().ContainInOrder("Ingresso à Pós-Graduação", "SISU 2026.1", "Vestibular de Música");
    }

    [Fact(DisplayName = "A ordenação decrescente inverte de fato a consulta")]
    public async Task Ordenacao_Decrescente_Inverte()
    {
        IReadOnlyList<string> nomes = await ListarAsync(
            new RecorteDaVitrine(), [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Descending)]);

        nomes.Should().ContainInOrder("Vestibular de Música", "SISU 2026.1", "Ingresso à Pós-Graduação");
    }

    [Fact(DisplayName = "Sem ordenação pedida, vale a ordem por urgência")]
    public async Task ListarVitrine_QuandoNaoPedeOrdenacao_DeveValerAOrdemPorUrgencia()
    {
        IReadOnlyList<string> nomes = await ListarAsync(new RecorteDaVitrine());

        nomes.Should().ContainInOrder("Ingresso à Pós-Graduação", "SISU 2026.1", "Vestibular de Música");
    }

    [Fact(DisplayName = "Cursor emitido sob uma busca é recusado sob outra")]
    public async Task ListarVitrine_QuandoCursorVemDeOutroRecorte_DeveRecusarAContinuacao()
    {
        // A âncora é uma posição DENTRO de um conjunto. Aceita sob outro recorte, o seek partiria de
        // um valor que não existe naquele conjunto e a página voltaria vazia — indistinguível de fim
        // de coleção, que é o pior desfecho possível: silencioso e plausível.
        (string SortKey, Guid Id)? proximo = await PrimeiraPaginaAsync(new RecorteDaVitrine(Busca: "a"), limite: 1);

        proximo.Should().NotBeNull("a busca precisa ter mais de uma página para o cursor existir");

        Func<Task> continuarSobOutroRecorte = () => ListarAsync(
            new RecorteDaVitrine(Busca: "sisu"), [], proximo!.Value.SortKey, proximo.Value.Id);

        await continuarSobOutroRecorte.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "Cursor emitido sob uma ordenação é recusado sob outra")]
    public async Task ListarVitrine_QuandoCursorVemDeOutraOrdenacao_DeveRecusarAContinuacao()
    {
        SortField[] porNome = [new(CamposOrdenacaoDaVitrine.Nome, SortDirection.Ascending)];
        (string SortKey, Guid Id)? proximo = await PrimeiraPaginaAsync(new RecorteDaVitrine(), limite: 1, ordenacao: porNome);

        proximo.Should().NotBeNull();

        Func<Task> continuarNoutraOrdem = () => ListarAsync(
            new RecorteDaVitrine(),
            [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Descending)],
            proximo!.Value.SortKey,
            proximo.Value.Id);

        await continuarNoutraOrdem.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Theory(DisplayName = "A travessia página a página não repete nem omite certame")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListarVitrine_QuandoPercorrePaginaAPagina_NaoDeveRepetirNemOmitir(bool ordenacaoEscolhida)
    {
        // Uma página por vez é o caso que expõe a âncora: ela precisa nomear as MESMAS propriedades
        // pelas quais a consulta ordena, senão toda página a partir da segunda parte de um valor que
        // não é o daquelas colunas. O defeito não aparece enquanto tudo couber numa página só.
        IReadOnlyList<SortField> ordenacao = ordenacaoEscolhida
            ? [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Ascending)]
            : [];

        List<string> percorridos = [];
        (string SortKey, Guid Id)? cursor = null;

        for (int pagina = 0; pagina < 5; pagina++)
        {
            (IReadOnlyList<CertameDivulgado> itens, (string SortKey, Guid Id)? proximo) =
                await PaginaAsync(ordenacao, cursor, PaginationDirection.Next);

            if (itens.Count == 0)
            {
                break;
            }

            percorridos.AddRange(itens.Select(static c => c.Nome));

            if (proximo is null)
            {
                break;
            }

            cursor = proximo;
        }

        percorridos.Should().HaveCount(3, "a travessia inteira devolve cada certame uma vez");
        percorridos.Should().OnlyHaveUniqueItems();
        // Os dois regimes coincidem nesta amostra — o prazo cresce junto com o alfabeto —, e é de
        // propósito: o que se está provando é a travessia, não a ordem, que os testes de ordenação
        // já cobrem separadamente.
        percorridos.Should().Equal("Ingresso à Pós-Graduação", "SISU 2026.1", "Vestibular de Música");
    }

    [Fact(DisplayName = "Voltar uma página devolve exatamente a página anterior")]
    public async Task Travessia_ParaTras_VoltaAPaginaAnterior()
    {
        // A âncora de volta é a que a própria página emite — é assim que o cliente navega, pelo
        // header Link. Voltar por ela precisa cair de novo no item de onde se veio, e não num
        // vizinho: errar o lado da desigualdade do seek desloca a página em um.
        (IReadOnlyList<CertameDivulgado> primeira, (string SortKey, Guid Id)? anteriorDaPrimeira, (string SortKey, Guid Id)? aposPrimeira) =
            await PaginaCompletaAsync(cursor: null, PaginationDirection.Next);

        anteriorDaPrimeira.Should().BeNull("a primeira página não tem anterior");
        aposPrimeira.Should().NotBeNull();

        (IReadOnlyList<CertameDivulgado> segunda, (string SortKey, Guid Id)? anteriorDaSegunda, _) =
            await PaginaCompletaAsync(aposPrimeira, PaginationDirection.Next);

        segunda.Should().ContainSingle();
        anteriorDaSegunda.Should().NotBeNull();

        (IReadOnlyList<CertameDivulgado> voltando, _, _) =
            await PaginaCompletaAsync(anteriorDaSegunda, PaginationDirection.Prev);

        voltando.Select(static c => c.Nome).Should().Equal(primeira.Select(static c => c.Nome));
    }

    [Fact(DisplayName = "Título do comprimento máximo da origem é divulgável")]
    public async Task Nome_NoComprimentoDaOrigem_EDivulgavel()
    {
        // A faceta é uma CÓPIA do título do processo, e uma cópia mais curta que a origem recusa um
        // cadastro legítimo. Recusa no pior lugar possível: esta escrita é assíncrona, disparada
        // pelo registro do ato, e a falha não volta a ninguém — a mensagem morre na fila, a linha
        // nunca nasce, e como a existência da linha É a publicidade, o certame fica invisível com o
        // ato já registrado. Nem o público nem quem publicou teriam como perceber.
        string tituloNoLimite = new('M', 300);

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        context.CertamesDivulgados.Add(Divulgado(tituloNoLimite, "900/2026", ["AC"], Agora.AddDays(15)));

        Func<Task> divulgar = () => context.SaveChangesAsync(CancellationToken.None);

        await divulgar.Should().NotThrowAsync(
            "o título do processo aceita 300 caracteres, e a divulgação copia o que a origem aceita");
    }

    [Fact(DisplayName = "Os contadores respeitam a busca, e não só a situação")]
    public async Task ContarPorSituacao_QuandoHaBuscaAplicada_DeveContarSoOsCorrespondentes()
    {
        // O número exibido ao lado do filtro promete quantos itens aquele filtro traz sobre o que
        // está na tela. Contar a vitrine inteira sob uma busca aplicada é o rótulo mentir.
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        ContadoresDaVitrine contadores = await repository.ContarPorSituacaoAsync(
            Agora, new RecorteDaVitrine(Busca: "sisu"), Limiar, CancellationToken.None);

        (contadores.EmBreve + contadores.InscricoesAbertas + contadores.UltimosDias + contadores.Encerrados)
            .Should().Be(1);
    }

    private async Task<IReadOnlyList<string>> ListarAsync(
        RecorteDaVitrine recorte,
        IReadOnlyList<SortField>? ordenacao = null,
        string? afterSortKey = null,
        Guid? afterId = null)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        (IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo) pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, afterSortKey, afterId, 20,
                PaginationDirection.Next, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Nome)];
    }

    private async Task<(string SortKey, Guid Id)?> PrimeiraPaginaAsync(
        RecorteDaVitrine recorte,
        int limite,
        IReadOnlyList<SortField>? ordenacao = null)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        (IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo) pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, null, null, limite,
                PaginationDirection.Next, CancellationToken.None);

        return pagina.Proximo;
    }

    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Proximo)> PaginaAsync(
        IReadOnlyList<SortField> ordenacao,
        (string SortKey, Guid Id)? cursor,
        PaginationDirection direcao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        (IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo) pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), ordenacao, Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, CancellationToken.None);

        return (pagina.Itens, direcao == PaginationDirection.Prev ? pagina.Anterior : pagina.Proximo);
    }

    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        PaginaCompletaAsync((string SortKey, Guid Id)? cursor, PaginationDirection direcao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        (IReadOnlyList<CertameDivulgado> Itens, DateTimeOffset InstanteEfetivo, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo) pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), [], Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, CancellationToken.None);

        return (pagina.Itens, pagina.Anterior, pagina.Proximo);
    }

    private static CertameDivulgado Divulgado(
        string nome,
        string numero,
        IReadOnlyList<string> modalidades,
        DateTimeOffset inscricoesAte) =>
        CertameDivulgado.Criar(
            Guid.CreateVersion7(),
            numeroVersao: 1,
            Guid.CreateVersion7(),
            new string('a', 64),
            versaoProjecao: "1",
            new FacetasDoCertameDivulgado(nome, numero, modalidades, Agora.AddDays(-1), inscricoesAte),
            """{"nome":"documento"}""",
            Agora);
}
