namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirFormularioCommandHandlerTests
{
    [Fact(DisplayName = "Processo publicado recusa a mutação antes de conferir as paginas")]
    public async Task Handle_Publicado_PaginaInvalida_RecusaAMutacao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.Situacao))!.SetValue(processo, SituacaoDoProcessoSeletivo.Publicado);

        Result result = await DefinirAsync(
            processo, PapelDoFormularioDeTeste.Inscricao, faseId: null, titulo: null, EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir());

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "Processo inexistente mantém o erro sem campo")]
    public async Task Handle_ProcessoInexistente_RetornaErroSemCampo()
    {
        DefinirFormularioCommand command = new(Guid.NewGuid(), PapelDoFormularioDeTeste.Habilitacao.Codigo(), null, null, true, []);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result result = await DefinirFormularioCommandHandler.Handle(
            command, Substitute.For<IProcessoSeletivoRepository>(), reader, unitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
        await ConferirQueNadaFoiLidoNemGravadoAsync(reader, unitOfWork);
    }

    [Fact(DisplayName = "Processo publicado é recusado sem ler o catálogo de dados do candidato nem gravar")]
    public async Task Handle_ProcessoPublicado_RecusaSemLerOCatalogo()
    {
        ProcessoSeletivo processo = ApoioDaCascataDeRemanejamento.NovoProcessoPublicado();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DefinirFormularioCommand command = new(processo.Id, PapelDoFormularioDeTeste.Inscricao.Codigo(), null, null, true, []);

        Result result = await DefinirFormularioCommandHandler.Handle(command, repository, reader, unitOfWork, CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await ConferirQueNadaFoiLidoNemGravadoAsync(reader, unitOfWork);
    }

    private static async Task ConferirQueNadaFoiLidoNemGravadoAsync(IFatoCandidatoReader reader, IUnitOfWork unitOfWork)
    {
        await reader.DidNotReceive().ListarAsync(Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Título, fase fora do cronograma e página inválidos saem juntos na mesma recusa")]
    public async Task Handle_TituloFaseEPaginaInvalidos_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out _);

        Result result = await DefinirAsync(
            processo, PapelDoFormularioDeTeste.Habilitacao, Guid.NewGuid(), new string('a', 301), EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir());

        result.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["paginas[0].apresentacao.titulo", "titulo", "faseId"]);
    }

    [Fact(DisplayName = "Pagina sem a apresentação é recusada no título, no caminho da apresentação")]
    public async Task Handle_PaginaSemApresentacao_RecusaOTituloAusente()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(
            processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, titulo: null, EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").SemApresentacao().Construir());

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(("paginas[0].apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "Exibição de seção que cita fato fora do catálogo é recusada na pagina, junto das recusas de estrutura e de título")]
    public async Task Handle_ExibicaoComFatoDesconhecido_RecusaNaPagina()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        PaginaDoFormularioInput secao = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados")
            .ExibidaQuando([[new CondicaoPrecondicaoInput("FATO_INEXISTENTE", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement(true))]]).Construir();

        Result result = await DefinirAsync(
            processo, PapelDoFormularioDeTeste.Inscricao, faseDeInscricao, new string('a', 301), [secao], CatalogoDoConjuntoBasico.Com([]));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("paginas[0].exibicao", "PredicadoDnf.FatoDesconhecido"),
            ("titulo", EstruturaFormularioErrorCodes.TituloTamanho),
            ("paginas", "EstruturaFormulario.BlocoExigidoAusente"),
        ]);
    }

    [Fact(DisplayName = "A segunda pagina inválida é recusada no caminho enviado")]
    public async Task Handle_SegundaPaginaInvalida_RecusaNoCaminhoDasPaginas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null,
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).Construir(),
            EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 1).ComTitulo("").Construir());

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("paginas[1].apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "Página de tipo inválido e códigos repetidos nas outras saem juntos, na posição do pedido e na ordem das posições")]
    public async Task Handle_PaginaRecusadaECodigoRepetido_AcumulaNaOrdemDasPosicoes()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null,
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).ComTitulo("Dados").Construir(),
            PaginaDeTipoInvalido("OUTRA", 1),
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 2).ComTitulo("Dados").Construir());

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("paginas[1].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio),
            ("paginas[2].codigo", EstruturaFormularioErrorCodes.PaginaCodigoDuplicado));
    }

    [Fact(DisplayName = "Com página recusada, o formulário sem revisão e aceite não recebe a recusa do bloco exigido")]
    public async Task Handle_PaginaRecusadaSemRevisaoEAceite_NaoRecusaOBlocoExigido()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null,
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).ComTitulo("Dados").Construir(), PaginaDeTipoInvalido("OUTRA", 1));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[1].tipo", EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio));
    }

    [Fact(DisplayName = "Com página recusada, o item de uma seção que o pedido não traz não recebe a recusa de item fora de seção")]
    public async Task Handle_PaginaRecusadaEItemComSecao_NaoRecusaOItem()
    {
        ProcessoSeletivo processo = ProcessoComItemNaSecao("DADOS", out Guid faseDeInscricao);
        PaginaDoFormularioInput outra = EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 0).ComTitulo("Outra").Construir();

        Result recusada = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null, outra, PaginaDeTipoInvalido("FIM", 1));
        Result completa = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null, outra, Revisao(1));

        recusada.Errors.Select(static e => e.Error.Code).Should().Equal(EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio);
        completa.Errors.Select(static e => e.Error.Code).Should().Equal(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact(DisplayName = "Com página recusada, a exibição que cita fato de seção posterior continua sem ser conferida pela posição")]
    public async Task Handle_PaginaRecusadaEExibicaoPelaPosicao_NaoConfereAPosicao()
    {
        ProcessoSeletivo processo = ProcessoComItemNaSecao("POSTERIOR", out Guid faseDeInscricao);
        PaginaDoFormularioInput exibida = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).ComTitulo("Dados")
            .ExibidaQuando([[new CondicaoPrecondicaoInput("CONDICAO_DA_HABILITACAO", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement(true))]]).Construir();
        PaginaDoFormularioInput posterior = EntradaDaPaginaDoFormularioDeTeste.Secao("POSTERIOR", 1).ComTitulo("Posterior").Construir();
        IReadOnlyList<FatoCandidatoView> catalogo = [CondicaoDaHabilitacaoNoCatalogo()];

        Result recusada = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null, [exibida, posterior, PaginaDeTipoInvalido("FIM", 2)], catalogo);
        Result completa = await DefinirAsync(processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null, [exibida, posterior, Revisao(2)], catalogo);

        recusada.Errors.Select(static e => e.Error.Code).Should().Equal(EstruturaFormularioErrorCodes.TipoDePaginaObrigatorio);
        completa.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[0].exibicao", "GrafoFormulario.CitaFatoPosterior"));
    }

    private static FatoCandidatoView CondicaoDaHabilitacaoNoCatalogo() => CatalogoDoConjuntoBasico.Fatos.Single(static f => f.Codigo == "DESEJA_NOME_SOCIAL") with
    {
        Codigo = "CONDICAO_DA_HABILITACAO",
        Binding = "CAMPO_FORMULARIO:CONDICAO_DA_HABILITACAO",
    };

    private static PaginaDoFormularioInput PaginaDeTipoInvalido(string codigo, int ordem) =>
        EntradaDaPaginaDoFormularioDeTeste.Secao(codigo, ordem).ComTitulo("Página").Construir() with { Tipo = "secao" };

    private static PaginaDoFormularioInput Revisao(int ordem) =>
        EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", ordem, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir();

    /// <summary>O formulário de habilitação com as seções DADOS e POSTERIOR, a revisão e aceite, e um item na seção dada.</summary>
    private static ProcessoSeletivo ProcessoComItemNaSecao(string secao, out Guid faseDeInscricao)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out faseDeInscricao);
        Result definido = DefinirAsync(
            processo, PapelDoFormularioDeTeste.Habilitacao, faseDeInscricao, null,
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).ComTitulo("Dados").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.Secao("POSTERIOR", 1).ComTitulo("Posterior").Construir(),
            Revisao(2)).GetAwaiter().GetResult();
        definido.IsSuccess.Should().BeTrue(definido.Error?.Message);
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("CONDICAO_DA_HABILITACAO", 0).DoTipo(TipoDeCampo.SimOuNao).NaSecao(secao).Construir()],
            papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "Criar o formulário de inscrição põe a seção dos dados básicos em primeiro, com os itens dela")]
    public async Task Handle_CriarInscricao_TrazASecaoDosDadosBasicos()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        processo.RemoverFormulario(PapelDoFormularioDeTeste.Inscricao).IsSuccess.Should().BeTrue();
        PaginaDoFormularioInput[] paginas =
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir(),
        ];

        Result result = await DefinirAsync(processo, PapelDoFormularioDeTeste.Inscricao, faseDeInscricao, titulo: null, paginas, CatalogoDoConjuntoBasico.Fatos);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Paginas.OrderBy(static e => e.Ordem).Select(static e => e.Codigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.CodigoDaSecao, "DADOS", "REVISAO");
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(ConjuntoBasicoDaInscricao.Fatos);
    }

    [Fact(DisplayName = "A consequência da falta de envio do vocabulário é gravada no formulário")]
    public async Task Handle_ConsequenciaDoVocabulario_GravaNoFormulario()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirComConsequenciaAsync(processo, "ELIMINA_DO_PROCESSO", PaginasValidas());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.ConsequenciaDaFaltaDeEnvio.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact(DisplayName = "Redefinir o formulário sem consequência retira a que ele declarava")]
    public async Task Handle_SemConsequencia_RetiraADeclaracaoAnterior()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        (await DefinirComConsequenciaAsync(processo, "RETIRA_A_MODALIDADE", PaginasValidas())).IsSuccess.Should().BeTrue();

        Result result = await DefinirComConsequenciaAsync(processo, null, PaginasValidas());

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.ConsequenciaDaFaltaDeEnvio.Should().BeNull();
    }

    [Fact(DisplayName = "Consequência fora do vocabulário é recusada no campo e nada é gravado")]
    public async Task Handle_ConsequenciaForaDoVocabulario_RecusaESemGravar()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirComConsequenciaAsync(processo, "ABRE_PRAZO_PARA_REENVIO", PaginasValidas());

        FieldError recusa = result.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("consequenciaDaFaltaDeEnvio");
        recusa.Error.Code.Should().Be(FormularioProcessoErrorCodes.ConsequenciaDaFaltaDeEnvioInvalida);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao).Should().BeNull();
    }

    [Fact(DisplayName = "A consequência recusada e a página inválida saem juntas na mesma recusa")]
    public async Task Handle_ConsequenciaEPaginaInvalidas_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirComConsequenciaAsync(
            processo, "XPTO", [EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir()]);

        result.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["paginas[0].apresentacao.titulo", "consequenciaDaFaltaDeEnvio"]);
    }

    private static PaginaDoFormularioInput[] PaginasValidas() =>
    [
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
        EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir(),
    ];

    private static Task<Result> DefinirComConsequenciaAsync(ProcessoSeletivo processo, string? consequencia, IReadOnlyList<PaginaDoFormularioInput> paginas)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), null, null, true, paginas, consequencia),
            repository, Substitute.For<IFatoCandidatoReader>(), Substitute.For<IUnitOfWork>(), CancellationToken.None);
    }

    private static Task<Result> DefinirAsync(
        ProcessoSeletivo processo, PapelDoFormularioDeTeste papel, Guid? faseId, string? titulo, params PaginaDoFormularioInput[] paginas) =>
        DefinirAsync(processo, papel, faseId, titulo, paginas, catalogo: []);

    private static Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        PapelDoFormularioDeTeste papel,
        Guid? faseId,
        string? titulo,
        IReadOnlyList<PaginaDoFormularioInput> paginas,
        IReadOnlyList<FatoCandidatoView> catalogo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogo);
        return DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, papel.Codigo(), faseId, titulo, FormularioDeTeste.DeclaradoNoPapel(papel, faseId).Obrigatorio, paginas), repository, reader, Substitute.For<IUnitOfWork>(), CancellationToken.None);
    }
}
