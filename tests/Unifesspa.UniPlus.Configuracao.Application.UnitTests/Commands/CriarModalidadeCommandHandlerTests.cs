namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

public sealed class CriarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> Incondicional = [];

    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();
    private readonly IFatoCandidatoRepository _fatoRepository = Substitute.For<IFatoCandidatoRepository>();
    private readonly IPrecedenciaFaseRepository _precedenciaRepository = Substitute.For<IPrecedenciaFaseRepository>();

    public CriarModalidadeCommandHandlerTests()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato)]);
        _precedenciaRepository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> OptouPelasCotas(bool optou) =>
        [[Condicao(OpcaoPelasCotasDaLei.Fato, optou)]];

    private static CondicaoDoPredicadoInput Condicao(string fato, bool valor) =>
        new(fato, "IGUAL", JsonSerializer.SerializeToElement(valor));

    private const string CodigoInstitucional = "PSVR_AMPLA";

    private static CriarModalidadeCommand ComandoAmpla() =>
        new(CodigoInstitucional, Descricao: "Ampla concorrência do vestibular remanescente", NaturezaLegal: "AMPLA",
            RegraDeAplicabilidade: Incondicional);

    [Fact(DisplayName = "Código livre cria a modalidade, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(CodigoInstitucional, null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            ComandoAmpla(), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Código reservado ao catálogo legal fixo é recusado (409) antes de consultar o banco")]
    [InlineData("LB_PPI")]
    [InlineData("AC")]
    [InlineData("AC_PCD")]
    [InlineData("AC_I")]
    [InlineData("AC_Q")]
    [InlineData(" LB_PPI ")]
    public async Task Handle_CodigoReservado_RetornaConflito(string codigo)
    {
        var comando = new CriarModalidadeCommand(codigo, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CriacaoBloqueadaCodigoProtegido);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(CodigoInstitucional, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            ComandoAmpla(), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência de composição inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        _repository.CodigoExisteEntreVivosAsync("PSIQ_QUILOMBOLA", null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // RETIRA_DE exige origem; a origem "XPTO" não existe viva.
        var comando = new CriarModalidadeCommand(
            "PSIQ_QUILOMBOLA", NaturezaLegal: "AMPLA", ComposicaoVagas: "RETIRA_DE", ComposicaoOrigem: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Invariante de domínio (cota sem cascata) propaga o erro sem persistir")]
    public async Task Handle_DominioIncoerente_RetornaErroSemPersistir()
    {
        _repository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);

        var comando = new CriarModalidadeCommand(
            "PSIQ_QUILOMBOLA", NaturezaLegal: "COTA_RESERVADA", ComposicaoVagas: "DENTRO_DO_VR", RegraDeAplicabilidade: OptouPelasCotas(true));

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido propaga o erro sem consultar unicidade nem persistir — validação vence I/O")]
    public async Task Handle_CampoInvalido_RetornaErroSemConsultarBancoNemPersistir()
    {
        var comando = new CriarModalidadeCommand(CodigoInstitucional, NaturezaLegal: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaInvalida);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regra ausente é recusada como obrigatória sem persistir")]
    public async Task Handle_SemRegra_Recusa()
    {
        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            new CriarModalidadeCommand(CodigoInstitucional, NaturezaLegal: "AMPLA"),
            _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regra malformada se acumula com as recusas dos demais campos, sem a de regra ausente")]
    public async Task Handle_RegraMalformada_AcumulaComOsDemaisCampos()
    {
        var comando = new CriarModalidadeCommand(
            CodigoInstitucional, Descricao: new string('a', 301), NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[]]);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("descricao", ModalidadeErrorCodes.DescricaoTamanho),
            ("regraDeAplicabilidade", "ClausulaDnf.ClausulaVazia"),
        ]);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do catálogo se acumula com a do agregado, sem persistir")]
    public async Task Handle_RecusaDoCatalogo_AcumulaComADoAgregado()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato), FatoDesativado("PCD")]);
        var comando = new CriarModalidadeCommand(
            CodigoInstitucional, Descricao: new string('a', 301), NaturezaLegal: "ACAO_AFIRMATIVA",
            ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL", RegraRemanejamento: "DESTINO_UNICO", RemanejamentoDestino: "AC",
            RegraDeAplicabilidade: [[Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao("PCD", true)]]);

        Result<Guid> resultado = await CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(
            [ModalidadeErrorCodes.DescricaoTamanho, ModalidadeErrorCodes.RegraCitaFatoDesativado]);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    private static FatoCandidato Fato(string codigo) =>
        FatoCandidato.CriarDoAdministrador(
            codigo, codigo, null, DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO",
            EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, "Enquadramento na reserva de vagas.",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private static FatoCandidato FatoDesativado(string codigo)
    {
        FatoCandidato fato = Fato(codigo);
        fato.Desativar();
        return fato;
    }
}
