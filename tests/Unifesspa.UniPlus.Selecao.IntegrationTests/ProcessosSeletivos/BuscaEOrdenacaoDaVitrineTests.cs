namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

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

    /// <summary>Versão do documento público que a consulta sabe servir — a que os semeados gravam.</summary>
    private const string VersaoServida = "1";

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
        // Normalizar só um lado faz a busca não achar o que existe.
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
        // Sem escapar, '%' casaria com tudo.
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
        // Pelo ponto de código o 'à' viria depois de toda letra ASCII.
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
        // A âncora é posição dentro de um conjunto: sob outro recorte, a página volta vazia —
        // indistinguível de fim de coleção.
        (string SortKey, Guid Id)? emitido = await PrimeiraPaginaAsync(new RecorteDaVitrine(Busca: "a"), limite: 1);

        emitido.Should().NotBeNull("a busca precisa ter mais de uma página para o cursor existir");
        (string SortKey, Guid Id) ancora = emitido!.Value;

        Func<Task> continuarSobOutroRecorte = () => ListarAsync(
            new RecorteDaVitrine(Busca: "sisu"), [], ancora.SortKey, ancora.Id);

        await continuarSobOutroRecorte.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Fact(DisplayName = "Cursor emitido antes de uma retificação é recusado depois dela")]
    public async Task ListarVitrine_QuandoAColecaoAvancaNoMeioDoPercurso_DeveRecusarAContinuacao()
    {
        // O prazo é a chave por que a vitrine ordena, e ele MUDA. Uma retificação no meio do
        // percurso reposiciona um certame em relação à âncora: quem a cruza num sentido aparece
        // duas vezes, quem cruza no outro desaparece. Prosseguir com a âncora antiga entregaria
        // uma lista silenciosamente inconsistente — a ADR-0131 exige interromper e reiniciar.
        (string SortKey, Guid Id)? emitido = await PrimeiraPaginaAsync(new RecorteDaVitrine(), limite: 1);

        emitido.Should().NotBeNull();
        (string SortKey, Guid Id) ancora = emitido!.Value;

        // Uma divulgação avança: é o que o registro do ato de uma retificação faz.
        await using (SelecaoDbContext mutacao = _fixture.CreateDbContext())
        {
            CertameDivulgado divulgado = await mutacao.CertamesDivulgados.FirstAsync(CancellationToken.None);
            divulgado.TentarAvancar(
                divulgado.NumeroVersao + 1,
                Guid.CreateVersion7(),
                new string('b', 64),
                versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    divulgado.IdentificadorLegivel, divulgado.Nome, divulgado.Numero, divulgado.ModalidadesOfertadas,
                    divulgado.InscricoesDe, divulgado.InscricoesAte.AddDays(5)),
                divulgado.Certame,
                Agora.AddMinutes(1));

            await mutacao.SaveChangesAsync(CancellationToken.None);
        }

        Func<Task> continuarDepoisDaMudanca = () => ListarAsync(
            new RecorteDaVitrine(), [], ancora.SortKey, ancora.Id);

        await continuarDepoisDaMudanca.Should().ThrowAsync<CursorAnchorMismatchException>(
            "a âncora é uma posição na coleção que a emitiu, e essa coleção mudou");
    }

    [Fact(DisplayName = "Cursor é recusado quando entra certame divulgado antes do mais recente")]
    public async Task ListarVitrine_QuandoEntraCertameComInstanteAnterior_DeveRecusarAContinuacao()
    {
        // O instante da divulgação é lido do relógio antes da gravação, não na ordem em que as
        // transações confirmam: de duas materializações concorrentes, a que leu o relógio primeiro
        // pode confirmar por último, e aí ela entra na coleção sem mover o instante mais recente.
        // A contagem se move em toda inserção, qualquer que seja o instante que a linha carregue.
        (string SortKey, Guid Id)? emitido = await PrimeiraPaginaAsync(new RecorteDaVitrine(), limite: 1);

        emitido.Should().NotBeNull();
        (string SortKey, Guid Id) ancora = emitido!.Value;

        await using (SelecaoDbContext mutacao = _fixture.CreateDbContext())
        {
            mutacao.CertamesDivulgados.Add(Divulgado(
                "Ingresso ao Mestrado", "004/2026", ["AC"], Agora.AddDays(15), divulgadoEm: Agora.AddDays(-1)));
            await mutacao.SaveChangesAsync(CancellationToken.None);
        }

        Func<Task> continuarDepoisDaEntrada = () => ListarAsync(
            new RecorteDaVitrine(), [], ancora.SortKey, ancora.Id);

        await continuarDepoisDaEntrada.Should().ThrowAsync<CursorAnchorMismatchException>(
            "um certame a mais desloca as posições seguintes, mesmo trazendo instante anterior ao máximo");
    }

    [Fact(DisplayName = "Cursor emitido sob uma ordenação é recusado sob outra")]
    public async Task ListarVitrine_QuandoCursorVemDeOutraOrdenacao_DeveRecusarAContinuacao()
    {
        SortField[] porNome = [new(CamposOrdenacaoDaVitrine.Nome, SortDirection.Ascending)];
        (string SortKey, Guid Id)? emitido = await PrimeiraPaginaAsync(new RecorteDaVitrine(), limite: 1, ordenacao: porNome);

        emitido.Should().NotBeNull();
        (string SortKey, Guid Id) ancora = emitido!.Value;

        Func<Task> continuarNoutraOrdem = () => ListarAsync(
            new RecorteDaVitrine(),
            [new SortField(CamposOrdenacaoDaVitrine.Nome, SortDirection.Descending)],
            ancora.SortKey,
            ancora.Id);

        await continuarNoutraOrdem.Should().ThrowAsync<CursorAnchorMismatchException>();
    }

    [Theory(DisplayName = "A travessia página a página não repete nem omite certame")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListarVitrine_QuandoPercorrePaginaAPagina_NaoDeveRepetirNemOmitir(bool ordenacaoEscolhida)
    {
        // Uma página por vez: a âncora precisa nomear as mesmas propriedades que a consulta ordena,
        // e o defeito não aparece enquanto tudo couber numa página.
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
        // A âncora de volta é a que a página emite. Errar o lado da desigualdade do seek desloca
        // a página em um.
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
        // A faceta copia o título do processo. Mais curta que a origem, recusa cadastro legítimo
        // numa escrita assíncrona cuja falha não volta a ninguém — a linha nunca nasce, e sem
        // linha o certame não é público.
        string tituloNoLimite = new('M', 300);

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        context.CertamesDivulgados.Add(Divulgado(tituloNoLimite, "900/2026", ["AC"], Agora.AddDays(15)));

        Func<Task> divulgar = () => context.SaveChangesAsync(CancellationToken.None);

        await divulgar.Should().NotThrowAsync(
            "o título do processo aceita 300 caracteres, e a divulgação copia o que a origem aceita");
    }

    [Fact(DisplayName = "Busca, modalidade e situação recortam em conjunto, e não uma de cada vez")]
    public async Task ListarVitrine_QuandoCombinaBuscaModalidadeESituacao_DeveAplicarAsTres()
    {
        // Isolado, cada recorte passa mesmo que a consulta aplique só o último. A amostra tem um
        // alvo que satisfaz os três e vizinhos que falham em exatamente um.
        await using (SelecaoDbContext seed = _fixture.CreateDbContext())
        {
            seed.CertamesDivulgados.AddRange(
                // Alvo: casa busca, modalidade e situação.
                Divulgado("Processo Seletivo Especial", "010/2026", ["PCD"], Agora.AddDays(3)),
                // Mesma busca e modalidade, mas encerrado — falha só na situação.
                Divulgado("Processo Seletivo Especial", "011/2026", ["PCD"], Agora.AddDays(-3)),
                // Mesma busca e situação, outra modalidade.
                Divulgado("Processo Seletivo Especial", "012/2026", ["AC"], Agora.AddDays(3)),
                // Mesma modalidade e situação, outro título.
                Divulgado("Chamada Pública", "013/2026", ["PCD"], Agora.AddDays(3)));

            await seed.SaveChangesAsync(CancellationToken.None);
        }

        IReadOnlyList<string> numeros = await ListarNumerosAsync(new RecorteDaVitrine(
            Situacao: SituacaoDoCertame.UltimosDias, Modalidade: "PCD", Busca: "seletivo especial"));

        numeros.Should().Equal(
            ["010/2026"],
            "os três recortes se acumulam na mesma consulta — cada vizinho falha em exatamente um deles");
    }

    [Fact(DisplayName = "Os contadores respeitam a busca, e não só a situação")]
    public async Task ContarPorSituacao_QuandoHaBuscaAplicada_DeveContarSoOsCorrespondentes()
    {
        // O número ao lado do filtro promete o que aquele filtro traz sobre o que está na tela.
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        ContadoresDaVitrine contadores = (await repository.ListarVitrineAsync(
            Agora, new RecorteDaVitrine(Busca: "sisu"), [], Limiar, null, null, 20, PaginationDirection.Next,
            incluirContadores: true, VersaoServida, CancellationToken.None)).Contadores!.Value;

        (contadores.EmBreve + contadores.InscricoesAbertas + contadores.UltimosDias + contadores.Encerrados)
            .Should().Be(1);
    }

    private async Task<IReadOnlyList<string>> ListarNumerosAsync(RecorteDaVitrine recorte)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, [], Limiar, null, null, 20, PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Numero!)];
    }

    private async Task<IReadOnlyList<string>> ListarAsync(
        RecorteDaVitrine recorte,
        IReadOnlyList<SortField>? ordenacao = null,
        string? afterSortKey = null,
        Guid? afterId = null)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, afterSortKey, afterId, 20,
                PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return [.. pagina.Itens.Select(static c => c.Nome)];
    }

    private async Task<(string SortKey, Guid Id)?> PrimeiraPaginaAsync(
        RecorteDaVitrine recorte,
        int limite,
        IReadOnlyList<SortField>? ordenacao = null)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, recorte, ordenacao ?? [], Limiar, null, null, limite,
                PaginationDirection.Next, incluirContadores: false, VersaoServida, CancellationToken.None);

        return pagina.Proximo;
    }

    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Proximo)> PaginaAsync(
        IReadOnlyList<SortField> ordenacao,
        (string SortKey, Guid Id)? cursor,
        PaginationDirection direcao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), ordenacao, Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, incluirContadores: false, VersaoServida, CancellationToken.None);

        return (pagina.Itens, direcao == PaginationDirection.Prev ? pagina.Anterior : pagina.Proximo);
    }

    private async Task<(IReadOnlyList<CertameDivulgado> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)>
        PaginaCompletaAsync((string SortKey, Guid Id)? cursor, PaginationDirection direcao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        CertameDivulgadoRepository repository = new(context);

        PaginaDaVitrine pagina =
            await repository.ListarVitrineAsync(
                Agora, new RecorteDaVitrine(), [], Limiar, cursor?.SortKey, cursor?.Id, 1,
                direcao, incluirContadores: false, VersaoServida, CancellationToken.None);

        return (pagina.Itens, pagina.Anterior, pagina.Proximo);
    }

    private static CertameDivulgado Divulgado(
        string nome,
        string numero,
        IReadOnlyList<string> modalidades,
        DateTimeOffset inscricoesAte,
        DateTimeOffset? divulgadoEm = null) =>
        CertameDivulgado.Criar(
            Guid.CreateVersion7(),
            numeroVersao: 1,
            Guid.CreateVersion7(),
            new string('a', 64),
            versaoProjecao: "1",
            new FacetasDoCertameDivulgado(IdentificadoresDeTeste.Novo().Valor, nome, numero, modalidades, Agora.AddDays(-1), inscricoesAte),
            """{"nome":"documento"}""",
            divulgadoEm ?? Agora);
}
