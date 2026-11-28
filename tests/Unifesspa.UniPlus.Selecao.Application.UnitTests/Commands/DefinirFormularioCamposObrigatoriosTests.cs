namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Dizer se o formulário é obrigatório e enviar a lista de páginas são obrigatórios no corpo: a falta chega ao domínio,
/// é recusada com o código de campo obrigatório e acumula com as demais recusas (ADR-0125). A lista vazia declara nenhuma página.
/// </summary>
public sealed class DefinirFormularioCamposObrigatoriosTests
{
    private const string BlocoExigidoAusente = "EstruturaFormulario.BlocoExigidoAusente";

    [Fact(DisplayName = "Obrigatoriedade ausente é recusada no campo com o código de campo obrigatório e nada é gravado")]
    public async Task Handle_ObrigatorioAusente_RecusaNoCampoESemGravar()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: null, Paginas: PaginasValidas()), unitOfWork);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("obrigatorio", CodigosDeValidacao.Obrigatorio));
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao).Should().BeNull();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Obrigatoriedade ausente, página inválida, consequência recusada e título longo saem juntos, na ordem do pedido")]
    public async Task Handle_ObrigatorioAusenteEOutrasRecusas_AcumulaNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        PaginaDoFormularioInput[] paginas = [EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("").Construir()];

        Result result = await DefinirAsync(
            processo, new Pedido(Obrigatorio: null, paginas, Titulo: new string('a', 301), Consequencia: "XPTO"));

        result.Errors.Select(static e => e.Field).Should().Equal(
            "obrigatorio", "paginas[0].apresentacao.titulo", "consequenciaDaFaltaDeEnvio", "titulo");
    }

    [Fact(DisplayName = "Lista de páginas ausente é recusada no campo com o código de campo obrigatório e nada é gravado")]
    public async Task Handle_PaginasAusentes_RecusaNoCampoESemGravar()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: true, Paginas: null), unitOfWork);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas", CodigosDeValidacao.Obrigatorio));
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao).Should().BeNull();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Página sem ordem é recusada como obrigatória, no campo da página, e nada é gravado")]
    public async Task Handle_PaginaSemOrdem_RecusaComoObrigatoria()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        PaginaDoFormularioInput[] paginas = [PaginasValidas()[0] with { Ordem = null }, PaginasValidas()[1]];

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: true, Paginas: paginas), unitOfWork);

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas[0].ordem", CodigosDeValidacao.Obrigatorio));
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao).Should().BeNull();
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Lista de páginas ausente não apaga as páginas do formulário já gravado")]
    public async Task Handle_PaginasAusentes_MantemOFormularioGravado()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        (await DefinirAsync(processo, new Pedido(Obrigatorio: true, PaginasValidas()))).IsSuccess.Should().BeTrue();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: true, Paginas: null));

        result.IsFailure.Should().BeTrue();
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Paginas.Select(static p => p.Codigo).Should().BeEquivalentTo(["DADOS", "REVISAO"]);
    }

    [Fact(DisplayName = "Lista de páginas ausente não recebe as recusas que dependem dela, e as demais continuam")]
    public async Task Handle_PaginasAusentesComTituloEConsequenciaInvalidos_AcumulaSoAsIndependentes()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: true, Paginas: null, Titulo: new string('a', 301), Consequencia: "XPTO"));

        result.Errors.Select(static e => e.Field).Should().Equal("paginas", "consequenciaDaFaltaDeEnvio", "titulo");
        result.Errors.Select(static e => e.Error.Code).Should().NotContain(BlocoExigidoAusente);
    }

    [Fact(DisplayName = "Obrigatoriedade e lista de páginas ausentes saem juntas, nessa ordem")]
    public async Task Handle_ObrigatorioEPaginasAusentes_AcumulaNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: null, Paginas: null));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("obrigatorio", CodigosDeValidacao.Obrigatorio), ("paginas", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "Lista de páginas vazia declara nenhuma página: a recusa é a do bloco exigido, não a de campo obrigatório")]
    public async Task Handle_PaginasVazias_RecusaPeloBlocoExigido()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: true, Paginas: []));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("paginas", BlocoExigidoAusente));
    }

    [Fact(DisplayName = "Obrigatoriedade informada como falsa é uma declaração, e não uma ausência")]
    public async Task Handle_ObrigatorioFalso_DefineOFormulario()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result result = await DefinirAsync(processo, new Pedido(Obrigatorio: false, PaginasValidas()));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Obrigatorio.Should().BeFalse();
    }

    private static PaginaDoFormularioInput[] PaginasValidas() =>
    [
        EntradaDaPaginaDoFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir(),
        EntradaDaPaginaDoFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão e aceite").Construir(),
    ];

    private static Task<Result> DefinirAsync(ProcessoSeletivo processo, Pedido pedido, IUnitOfWork? unitOfWork = null)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns([]);
        return DefinirFormularioCommandHandler.Handle(
            new DefinirFormularioCommand(
                processo.Id, PapelDoFormularioDeTeste.Habilitacao.Codigo(), FaseId: null, pedido.Titulo, pedido.Obrigatorio, pedido.Paginas, pedido.Consequencia),
            repository, reader, unitOfWork ?? Substitute.For<IUnitOfWork>(), CancellationToken.None);
    }

    /// <summary>O corpo do pedido: o que o teste varia, com título e consequência ausentes por padrão.</summary>
    private sealed record Pedido(bool? Obrigatorio, IReadOnlyList<PaginaDoFormularioInput>? Paginas, string? Titulo = null, string? Consequencia = null);
}
