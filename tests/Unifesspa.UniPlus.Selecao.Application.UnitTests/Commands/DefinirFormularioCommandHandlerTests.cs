namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
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
            processo, FinalidadeFormulario.Inscricao, faseId: null, titulo: null, EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir());

        result.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "Processo inexistente mantém o erro sem campo")]
    public async Task Handle_ProcessoInexistente_RetornaErroSemCampo()
    {
        DefinirFormularioCommand command = new(Guid.NewGuid(), FinalidadeFormulario.Habilitacao, null, null, []);

        Result result = await DefinirFormularioCommandHandler.Handle(
            command, Substitute.For<IProcessoSeletivoRepository>(), Substitute.For<IFatoCandidatoReader>(),
            Substitute.For<IUnitOfWork>(), CancellationToken.None);

        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        result.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "Título, fase e pagina inválidos saem juntos na mesma recusa")]
    public async Task Handle_TituloFaseEPaginaInvalidos_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(
            processo, FinalidadeFormulario.Habilitacao, faseDeInscricao, new string('a', 301), EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir());

        result.Errors.Select(static e => e.Field).Should().BeEquivalentTo(["paginas[0].apresentacao.titulo", "titulo", "faseId"]);
    }

    [Fact(DisplayName = "Pagina sem a apresentação é recusada no título, no caminho da apresentação")]
    public async Task Handle_PaginaSemApresentacao_RecusaOTituloAusente()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);

        Result result = await DefinirAsync(
            processo, FinalidadeFormulario.Habilitacao, faseDeInscricao, titulo: null, EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").SemApresentacao().Construir());

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(("paginas[0].apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "Exibição de seção que cita fato fora do catálogo é recusada na pagina, junto das recusas de estrutura e de título")]
    public async Task Handle_ExibicaoComFatoDesconhecido_RecusaNaPagina()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        PaginaDoFormularioInput secao = EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados")
            .ExibidaQuando([[new CondicaoPrecondicaoInput("FATO_INEXISTENTE", "IGUAL", System.Text.Json.JsonSerializer.SerializeToElement(true))]]).Construir();

        Result result = await DefinirAsync(
            processo, FinalidadeFormulario.Inscricao, faseDeInscricao, new string('a', 301), [secao], CatalogoDoConjuntoBasico.Com([]));

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

        Result result = await DefinirAsync(processo, FinalidadeFormulario.Habilitacao, faseDeInscricao, null,
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS", 0).Construir(),
            EntradaDaPaginaDoFormularioDeTeste.Secao("OUTRA", 1).ComTitulo("").Construir());

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should()
            .Contain(("paginas[1].apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "Criar o formulário de inscrição põe a seção dos dados básicos em primeiro, com os itens dela")]
    public async Task Handle_CriarInscricao_TrazASecaoDosDadosBasicos()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Formulário", out Guid faseDeInscricao);
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao).IsSuccess.Should().BeTrue();
        PaginaDoFormularioInput[] paginas =
        [
            EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
            EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir(),
        ];

        Result result = await DefinirAsync(processo, FinalidadeFormulario.Inscricao, faseDeInscricao, titulo: null, paginas, CatalogoDoConjuntoBasico.Fatos);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Paginas.OrderBy(static e => e.Ordem).Select(static e => e.Codigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.CodigoDaSecao, "DADOS", "REVISAO");
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().BeEquivalentTo(ConjuntoBasicoDaInscricao.Fatos);
    }

    private static Task<Result> DefinirAsync(
        ProcessoSeletivo processo, FinalidadeFormulario finalidade, Guid? faseId, string? titulo, params PaginaDoFormularioInput[] paginas) =>
        DefinirAsync(processo, finalidade, faseId, titulo, paginas, catalogo: []);

    private static Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        FinalidadeFormulario finalidade,
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
            new DefinirFormularioCommand(processo.Id, finalidade, faseId, titulo, paginas), repository, reader, Substitute.For<IUnitOfWork>(), CancellationToken.None);
    }
}
