namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;

public sealed class CriarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> Incondicional = [];

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
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
        new(CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, Descricao: "Ampla concorrência do vestibular remanescente", NaturezaLegal: "AMPLA",
            RegraDeAplicabilidade: Incondicional);

    private Task<Result<Guid>> Criar(CriarModalidadeCommand comando) =>
        CriarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

    [Fact(DisplayName = "Código livre cria a modalidade, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(CodigoInstitucional, null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await Criar(ComandoAmpla());

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
    public async Task Handle_CodigoReservado_RetornaConflito(string codigo)
    {
        var comando = new CriarModalidadeCommand(codigo, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CriacaoBloqueadaCodigoProtegido);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código reservado com espaços é recusado pelo formato")]
    public async Task Handle_CodigoReservadoComEspacos_RecusaFormato()
    {
        var comando = new CriarModalidadeCommand(" LB_PPI ", Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

        resultado.Errors.Should().Contain(e => e.Error.Code == ModalidadeErrorCodes.CodigoFormatoInvalido);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(CodigoInstitucional, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await Criar(ComandoAmpla());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome já existente entre vivos retorna conflito (NomeJaExiste) sem persistir")]
    public async Task Handle_NomeDuplicado_RetornaConflito()
    {
        _repository.NomeExisteEntreVivosAsync(ModalidadesDeTeste.Nome, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await Criar(ComandoAmpla());

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O código já existente é recusado antes do nome já existente")]
    public async Task Handle_CodigoENomeDuplicados_RecusaOCodigoPrimeiro()
    {
        _repository.CodigoExisteEntreVivosAsync(CodigoInstitucional, null, Arg.Any<CancellationToken>()).Returns(true);
        _repository.NomeExisteEntreVivosAsync(ModalidadesDeTeste.Nome, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await Criar(ComandoAmpla());

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoJaExiste);
    }

    [Fact(DisplayName = "O nome já existente é recusado antes da referência inexistente")]
    public async Task Handle_NomeDuplicadoEReferenciaInexistente_RecusaONomePrimeiro()
    {
        _repository.NomeExisteEntreVivosAsync(ModalidadesDeTeste.Nome, null, Arg.Any<CancellationToken>()).Returns(true);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(false);
        var comando = new CriarModalidadeCommand(
            CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA",
            ComposicaoVagas: "RETIRADA_DE_OUTRA_MODALIDADE", ComposicaoOrigem: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
    }

    [Fact(DisplayName = "Nome ausente é recusado junto do código inválido, sem consultar o banco")]
    public async Task Handle_NomeAusenteECodigoInvalido_AcumulaAsDuasRecusas()
    {
        var comando = new CriarModalidadeCommand("lb-ppi", Nome: null, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

        resultado.Errors.Select(static e => e.Field).Should().Equal("codigo", "nome");
        await _repository.DidNotReceive().NomeExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida pelo índice do nome vira NomeJaExiste e descarta o que não foi salvo")]
    public async Task Handle_CorridaPeloIndiceDoNome_RetornaNomeJaExisteEDescarta()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException(
            "violação sintética de teste", PostgresExceptionFactory.Create("23505", RestricoesDoBanco.ModalidadeNomeAtivos)));

        Result<Guid> resultado = await Criar(ComandoAmpla());

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Corrida pelo índice do código vira CodigoJaExiste e descarta o que não foi salvo")]
    public async Task Handle_CorridaPeloIndiceDoCodigo_RetornaCodigoJaExisteEDescarta()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException(
            "violação sintética de teste", PostgresExceptionFactory.Create("23505", RestricoesDoBanco.ModalidadeCodigoAtivos)));

        Result<Guid> resultado = await Criar(ComandoAmpla());

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outro índice único propaga sem descartar")]
    public async Task Handle_CorridaPorOutroIndice_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException(
            "violação sintética de teste", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => Criar(ComandoAmpla());

        await acao.Should().ThrowAsync<DbUpdateException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Referência de composição inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        _repository.CodigoExisteEntreVivosAsync("PSIQ_QUILOMBOLA", null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // RETIRADA_DE_OUTRA_MODALIDADE exige origem; a origem "XPTO" não existe viva.
        var comando = new CriarModalidadeCommand(
            "PSIQ_QUILOMBOLA", Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA", ComposicaoVagas: "RETIRADA_DE_OUTRA_MODALIDADE", ComposicaoOrigem: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

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
            "PSIQ_QUILOMBOLA", Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "COTA_RESERVADA", ComposicaoVagas: "DENTRO_DA_RESERVA_LEGAL", RegraDeAplicabilidade: OptouPelasCotas(true));

        Result<Guid> resultado = await Criar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido propaga o erro sem consultar unicidade nem persistir — validação vence I/O")]
    public async Task Handle_CampoInvalido_RetornaErroSemConsultarBancoNemPersistir()
    {
        var comando = new CriarModalidadeCommand(CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result<Guid> resultado = await Criar(comando);

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
            new CriarModalidadeCommand(CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA"),
            _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(ModalidadeErrorCodes.RegraDeAplicabilidadeObrigatoria);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regra malformada se acumula com as recusas dos demais campos, sem a de regra ausente")]
    public async Task Handle_RegraMalformada_AcumulaComOsDemaisCampos()
    {
        var comando = new CriarModalidadeCommand(
            CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, Descricao: new string('a', 301), NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[]]);

        Result<Guid> resultado = await Criar(comando);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("descricao", ModalidadeErrorCodes.DescricaoTamanho),
            ("regraDeAplicabilidade[0]", "ClausulaDnf.ClausulaVazia"),
        ]);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do catálogo se acumula com a do agregado, sem persistir")]
    public async Task Handle_RecusaDoCatalogo_AcumulaComADoAgregado()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato), FatoDesativado("PCD")]);
        var comando = new CriarModalidadeCommand(
            CodigoInstitucional, Nome: ModalidadesDeTeste.Nome, Descricao: new string('a', 301), NaturezaLegal: "ACAO_AFIRMATIVA",
            ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL", Remanejamento: new("DESTINO_UNICO", Destino: "AC"),
            RegraDeAplicabilidade: [[Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao("PCD", true)]]);

        Result<Guid> resultado = await Criar(comando);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(
            [ModalidadeErrorCodes.DescricaoTamanho, ModalidadeErrorCodes.RegraCitaFatoDesativado]);
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.DescricaoTamanho);
        resultado.Errors.Single(static e => e.Error.Code == ModalidadeErrorCodes.RegraCitaFatoDesativado)
            .Field.Should().Be("regraDeAplicabilidade[0][1].fato");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<Modalidade>(), Arg.Any<CancellationToken>());
    }

    private static FatoCandidato Fato(string codigo) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Sensivel, "Enquadramento na reserva de vagas.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)).Value!;

    private static FatoCandidato FatoDesativado(string codigo)
    {
        FatoCandidato fato = Fato(codigo);
        fato.Desativar();
        return fato;
    }
}
