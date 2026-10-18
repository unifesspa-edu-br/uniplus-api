namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Leitura pública do certame, detalhe e vitrine, sobre a tabela de divulgações.
/// </summary>
/// <remarks>
/// A existência da linha É a publicidade, e é isso que estes testes exercitam: não há critério de
/// visibilidade a aplicar na leitura, porque quem não é público não tem linha.
/// </remarks>
public sealed class LeituraPublicaDoCertameTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Certame sem divulgação responde não encontrado")]
    public async Task Detalhe_SemDivulgacao_NaoEncontrado()
    {
        // Inexistente, rascunho, sem versão vigente e com ato não confirmado caem aqui pelo mesmo
        // caminho: nenhum deles tem linha. Distinguir deixou de ser possível, em vez de ser
        // possível e proibido.
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CertameDivulgado?)null);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(Guid.CreateVersion7(), Agora), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Certame divulgado devolve a projeção que foi materializada")]
    public async Task Detalhe_ComDivulgacao_DevolveAProjecao()
    {
        Guid processoId = Guid.CreateVersion7();
        CertamePublicadoDto esperado = Projecao(processoId);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>())
            .Returns(Divulgado(processoId, esperado));

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.AtoCriadorId.Should().Be(esperado.AtoCriadorId);
        resultado.Value.Periodo.Numero.Should().Be("001/2026");
        resultado.Value.Nome.Should().Be("SISU 2026.1", "o título é congelado na divulgação, não lido do agregado vivo");
    }

    [Theory(DisplayName = "O detalhe traz a situação da janela no instante da leitura, pela regra da vitrine")]
    [InlineData(5, 30, SituacaoDoCertame.EmBreve)]
    [InlineData(-1, 30, SituacaoDoCertame.InscricoesAbertas)]
    [InlineData(-1, 3, SituacaoDoCertame.UltimosDias)]
    [InlineData(-30, -1, SituacaoDoCertame.Encerradas)]
    public async Task Detalhe_TrazASituacaoDoInstante(int diasAteAbrir, int diasAteFechar, SituacaoDoCertame esperada)
    {
        Guid processoId = Guid.CreateVersion7();
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>())
            .Returns(Divulgado(
                processoId, Projecao(processoId),
                inscricoesDe: Agora.AddDays(diasAteAbrir), inscricoesAte: Agora.AddDays(diasAteFechar)));

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.Value!.Situacao.Should().Be(esperada);
    }

    [Fact(DisplayName = "A situação não entra no documento divulgado: é calculada na leitura")]
    public void Situacao_NaoEntraNoDocumentoDivulgado()
    {
        CertamePublicadoDto lido = Projecao(Guid.CreateVersion7()) with { Situacao = SituacaoDoCertame.InscricoesAbertas };

        string documento = JsonSerializer.Serialize(lido with { Situacao = null }, ProjecaoDoCertamePublicado.OpcoesDoDocumento);

        documento.Should().NotContain("situacao");
    }

    [Fact(DisplayName = "O selo do detalhe muda quando a situação muda, com o mesmo documento divulgado")]
    public void Selo_MudaComASituacao()
    {
        CertamePublicadoDto certame = Projecao(Guid.CreateVersion7());

        string aberto = SeloDoCertamePublicado.De(certame with { Situacao = SituacaoDoCertame.InscricoesAbertas });
        string ultimosDias = SeloDoCertamePublicado.De(certame with { Situacao = SituacaoDoCertame.UltimosDias });

        ultimosDias.Should().NotBe(aberto);
    }

    [Fact(DisplayName = "O detalhe traz o edital de cada versão divulgada, da mais antiga à mais recente")]
    public async Task Detalhe_TrazOsEditaisPorVersao()
    {
        Guid processoId = Guid.CreateVersion7();
        CertameDivulgado divulgado = Divulgado(processoId, Projecao(processoId));
        EditalDivulgado retificacao = new(Guid.CreateVersion7(), 2, "https://acervo.teste/retificacao.pdf");
        EditalDivulgado abertura = new(Guid.CreateVersion7(), 1, "https://acervo.teste/abertura.pdf");
        divulgado.RegistrarEdital(retificacao);
        divulgado.RegistrarEdital(abertura);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.Value!.EditaisPublicados!.Select(static e => (e.AtoId, e.UrlDownload.OriginalString)).Should().Equal(
            (abertura.AtoId, abertura.UrlDownload),
            (retificacao.AtoId, retificacao.UrlDownload));
    }

    [Fact(DisplayName = "O ato que não criou versão vem depois das versões, por ordem estável")]
    public async Task Detalhe_PoeOAtoSemVersaoDepoisDasVersoes()
    {
        Guid processoId = Guid.CreateVersion7();
        CertameDivulgado divulgado = Divulgado(processoId, Projecao(processoId));
        EditalDivulgado aviso = new(Guid.CreateVersion7(), null, "https://acervo.teste/aviso.pdf");
        EditalDivulgado abertura = new(Guid.CreateVersion7(), 1, "https://acervo.teste/abertura.pdf");
        divulgado.RegistrarEdital(aviso);
        divulgado.RegistrarEdital(abertura);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.Value!.EditaisPublicados!.Select(static e => e.AtoId).Should().Equal(abertura.AtoId, aviso.AtoId);
    }

    [Fact(DisplayName = "O edital divulgado devolve o principal e os anexos, com o registro antigo sem documentos")]
    public async Task Detalhe_TrazOsDocumentosDeCadaEdital()
    {
        Guid processoId = Guid.CreateVersion7();
        CertameDivulgado divulgado = Divulgado(processoId, Projecao(processoId));
        EditalDivulgado antigo = new(Guid.CreateVersion7(), 1, "https://acervo.teste/abertura.pdf");
        EditalDivulgado comAnexos = new(
            Guid.CreateVersion7(),
            2,
            "https://acervo.teste/retificacao.pdf",
            [
                new DocumentoDivulgado(PapelDoDocumentoDivulgado.Principal, "edital.pdf", "application/pdf", 1200, new string('a', 64), "https://acervo.teste/retificacao.pdf"),
                new DocumentoDivulgado(PapelDoDocumentoDivulgado.Anexo, "Quadro de vagas.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 800, new string('b', 64), "https://acervo.teste/quadro.docx"),
                new DocumentoDivulgado(PapelDoDocumentoDivulgado.Anexo, "Sem endereço.pdf", "application/pdf", 10, new string('c', 64), "acervo/sem-esquema.pdf"),
            ]);
        divulgado.RegistrarEdital(antigo);
        divulgado.RegistrarEdital(comAnexos);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        IReadOnlyList<EditalPublicadoCertameDto> editais = resultado.Value!.EditaisPublicados!;
        editais[0].Documentos.Should().BeEmpty();
        editais[1].Documentos.Select(static d => (d.Papel, d.Nome, d.TamanhoBytes, d.UrlDownload.OriginalString)).Should().Equal(
            ("principal", "edital.pdf", 1200L, "https://acervo.teste/retificacao.pdf"),
            ("anexo", "Quadro de vagas.docx", 800L, "https://acervo.teste/quadro.docx"));
    }

    [Fact(DisplayName = "Edital com endereço ilegível fica fora da lista sem derrubar o detalhe")]
    public async Task Detalhe_EditalComEnderecoIlegivel_FicaForaDaLista()
    {
        Guid processoId = Guid.CreateVersion7();
        CertameDivulgado divulgado = Divulgado(processoId, Projecao(processoId));
        EditalDivulgado abertura = new(Guid.CreateVersion7(), 1, "acervo/sem-esquema.pdf");
        EditalDivulgado retificacao = new(Guid.CreateVersion7(), 2, "https://acervo.teste/retificacao.pdf");
        divulgado.RegistrarEdital(abertura);
        divulgado.RegistrarEdital(retificacao);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EditaisPublicados!.Select(static e => e.AtoId).Should().Equal(retificacao.AtoId);
    }

    [Fact(DisplayName = "Documento divulgado antes dos editais por versão continua legível e responde com a lista")]
    public async Task Detalhe_DocumentoSemOCampoNovo_ContinuaLegivel()
    {
        // O documento guardado é o de antes desta coluna: não tem a propriedade, e a leitura não
        // pode tratá-la como obrigatória nem deixá-lo ilegível.
        Guid processoId = Guid.CreateVersion7();
        string documento = JsonSerializer.Serialize(Projecao(processoId), ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        documento.Should().NotContain("editaisPublicados");
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>())
            .Returns(Divulgado(processoId, Projecao(processoId)));

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.EditaisPublicados.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName = "O selo do detalhe muda quando entra um edital, com o mesmo documento divulgado")]
    public void Selo_MudaQuandoEntraEdital()
    {
        CertamePublicadoDto certame = Projecao(Guid.CreateVersion7()) with { Situacao = SituacaoDoCertame.InscricoesAbertas };
        EditalPublicadoCertameDto abertura = new(Guid.CreateVersion7(), new Uri("https://acervo.teste/abertura.pdf"), []);
        EditalPublicadoCertameDto retificacao = new(Guid.CreateVersion7(), new Uri("https://acervo.teste/retificacao.pdf"), []);

        string um = SeloDoCertamePublicado.De(certame with { EditaisPublicados = [abertura] });
        string dois = SeloDoCertamePublicado.De(certame with { EditaisPublicados = [abertura, retificacao] });

        dois.Should().NotBe(um);
        SeloDoCertamePublicado.De(certame with { EditaisPublicados = [abertura] }).Should().Be(um, "o selo é estável para a mesma lista");
    }

    [Fact(DisplayName = "O selo do detalhe muda quando muda um anexo do edital, com o mesmo endereço do edital")]
    public void Selo_MudaQuandoMudaUmAnexo()
    {
        CertamePublicadoDto certame = Projecao(Guid.CreateVersion7()) with { Situacao = SituacaoDoCertame.InscricoesAbertas };
        Guid ato = Guid.CreateVersion7();
        DocumentoDoAtoCertameDto anexo = new("anexo", "Quadro.pdf", "application/pdf", 10, new string('a', 64), new Uri("https://acervo.teste/q.pdf"));
        EditalPublicadoCertameDto semAnexo = new(ato, new Uri("https://acervo.teste/e.pdf"), []);

        string sem = SeloDoCertamePublicado.De(certame with { EditaisPublicados = [semAnexo] });
        string com = SeloDoCertamePublicado.De(certame with { EditaisPublicados = [semAnexo with { Documentos = [anexo] }] });
        string trocado = SeloDoCertamePublicado.De(certame with { EditaisPublicados = [semAnexo with { Documentos = [anexo with { HashSha256 = new string('b', 64) }] }] });

        com.Should().NotBe(sem);
        trocado.Should().NotBe(com);
    }

    [Fact(DisplayName = "A vitrine devolve a página inteira: não há descarte depois de formada")]
    public async Task Handle_QuandoHaVariosDivulgados_DeveDevolverAPaginaInteira()
    {
        Guid a = Guid.CreateVersion7();
        Guid b = Guid.CreateVersion7();
        ICertameDivulgadoRepository repository = RepositorioComVitrine(a, b);

        ListarCertamesPublicadosResult resultado = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;

        resultado.Items.Should().HaveCount(2, "só há linha para certame público, então nada é filtrado depois");
        resultado.Items.Select(static i => i.ProcessoSeletivoId).Should().ContainInOrder(a, b);
    }

    [Theory(DisplayName = "A situação do item é resolvida no servidor, contra o instante da navegação")]
    [InlineData(5, 30, SituacaoDoCertame.EmBreve)]
    [InlineData(-1, 30, SituacaoDoCertame.InscricoesAbertas)]
    [InlineData(-1, 3, SituacaoDoCertame.UltimosDias)]
    [InlineData(-30, -1, SituacaoDoCertame.Encerradas)]
    public async Task Handle_QuandoJanelaEmCadaPonto_DeveMarcarASituacaoDoInstante(int diasAteAbrir, int diasAteFechar, SituacaoDoCertame esperada)
    {
        // O fuso de quem lê não decide prazo de edital, e a janela tem dois lados: um edital
        // publicado antes de a inscrição abrir não está recebendo inscrição.
        Guid processoId = Guid.CreateVersion7();
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        CertameDivulgado linha = Divulgado(
            processoId, Projecao(processoId),
            inscricoesDe: Agora.AddDays(diasAteAbrir), inscricoesAte: Agora.AddDays(diasAteFechar));
        repository.ListarVitrineAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<RecorteDaVitrine>(), Arg.Any<IReadOnlyList<SortField>>(),
                Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(),
                Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaginaDaVitrine([linha], Agora, null, null, null, RevisaoDoRecorte));

        ListarCertamesPublicadosResult resultado = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;

        resultado.Items.Should().ContainSingle().Which.Situacao.Should().Be(esperada);
    }

    [Fact(DisplayName = "Contadores só são calculados quando pedidos")]
    public async Task Handle_QuandoNaoPedeContadores_DeveOmitiLos()
    {
        // Contar é percurso a mais sobre a coleção inteira, e o cursor preserva o parâmetro: quem
        // pede uma vez pagaria em toda página. Quem decide é a consulta, e o repositório responde
        // ao que ela pediu — por isso o dublê devolve os números só quando o sinalizador chega.
        ICertameDivulgadoRepository repository = RepositorioComVitrine(Guid.CreateVersion7());

        ListarCertamesPublicadosResult sem = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;
        ListarCertamesPublicadosResult com = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(incluirContadores: true), repository, CancellationToken.None)).Value!;

        sem.Contadores.Should().BeNull();
        com.Contadores.Should().Be(new ContadoresDaVitrine(5, 12, 3, 40));
    }

    [Fact(DisplayName = "A revisão do recorte passa do repositório ao resultado sem alteração")]
    public async Task Handle_DeveRepassarARevisaoDoRecorte()
    {
        // O valor é opaco para a aplicação: quem o deriva é a consulta, que enxerga o recorte
        // inteiro. Recalculá-lo ou omiti-lo aqui faria o cabeçalho da vitrine deixar de avisar que
        // a coleção avançou.
        ICertameDivulgadoRepository repository = RepositorioComVitrine(Guid.CreateVersion7());

        ListarCertamesPublicadosResult resultado = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;

        resultado.Revisao.Should().Be(RevisaoDoRecorte);
    }

    private const string RevisaoDoRecorte = "revisao-do-recorte";

    /// <summary>Posição de <c>incluirContadores</c> na chamada ao repositório.</summary>
    private const int PosicaoDoSinalizadorDeContadores = 8;

    private static ListarCertamesPublicadosQuery Consulta(bool incluirContadores = false) =>
        new(Agora, new RecorteDaVitrine(), [], null, null, 20, PaginationDirection.Next, incluirContadores);

    private static ICertameDivulgadoRepository RepositorioComVitrine(params Guid[] processoIds)
    {
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        CertameDivulgado[] linhas = [.. processoIds.Select(id => Divulgado(id, Projecao(id)))];
        repository.ListarVitrineAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<RecorteDaVitrine>(), Arg.Any<IReadOnlyList<SortField>>(),
                Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(),
                Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => new PaginaDaVitrine(
                linhas,
                Agora,
                null,
                ("ancora", processoIds[^1]),
                chamada.ArgAt<bool>(PosicaoDoSinalizadorDeContadores) ? new ContadoresDaVitrine(5, 12, 3, 40) : null,
                RevisaoDoRecorte));
        return repository;
    }

    private static CertameDivulgado Divulgado(
        Guid processoId,
        CertamePublicadoDto projecao,
        DateTimeOffset? inscricoesDe = null,
        DateTimeOffset? inscricoesAte = null) =>
        CertameDivulgado.Criar(
            processoId,
            numeroVersao: 1,
            projecao.AtoCriadorId,
            new string('a', 64),
            ProjecaoDoCertamePublicado.Versao,
            new FacetasDoCertameDivulgado(
                projecao.IdentificadorLegivel,
                projecao.Nome,
                projecao.Periodo.Numero,
                projecao.ModalidadesOfertadas,
                inscricoesDe ?? Agora.AddDays(-1),
                inscricoesAte ?? Agora.AddDays(20)),
            JsonSerializer.Serialize(projecao, ProjecaoDoCertamePublicado.OpcoesDoDocumento),
            Agora);

    private static CertamePublicadoDto Projecao(Guid processoId) => new(
        processoId,
        IdentificadoresDeTeste.Novo().Valor,
        Guid.CreateVersion7(),
        "SISU 2026.1",
        ProjecaoDoCertamePublicado.Versao,
        new string('a', 64),
        new TipoCatalogadoCertameDto("SISU", "Sistema de Seleção Unificada"),
        new PeriodoInscricaoCertameDto("001/2026", Agora.AddDays(-1), Agora.AddDays(20)),
        new LocalidadeCertameDto("1504208", "Marabá", "PA", FusoHorarioDto.Institucional),
        new UnidadeAdministradoraCertameDto("UNIFESSPA", "Universidade", "Autarquia", "Marabá", "PA"),
        new DocumentoEditalCertameDto(Guid.CreateVersion7(), new string('b', 64), new Uri("https://acervo.teste/edital.pdf")),
        [],
        ["AC"],
        [new QuadroDeVagasCertameDto(Guid.CreateVersion7(), [], 100)],
        [],
        "Externa",
        [],
        [],
        new AtendimentoCertameDto([], [], []),
        null,
        null);
}
