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
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class AtualizarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> Incondicional = [];

    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();
    private readonly IFatoCandidatoRepository _fatoRepository = Substitute.For<IFatoCandidatoRepository>();
    private readonly IPrecedenciaFaseRepository _precedenciaRepository = Substitute.For<IPrecedenciaFaseRepository>();

    public AtualizarModalidadeCommandHandlerTests()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato)]);
        _precedenciaRepository.ListarVivasAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private static IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> OptouPelasCotas(bool optou) =>
        [[Condicao(OpcaoPelasCotasDaLei.Fato, optou)]];

    private static CondicaoDoPredicadoInput Condicao(string fato, bool valor) =>
        new(fato, "IGUAL", JsonSerializer.SerializeToElement(valor));

    private static Modalidade Existente(string codigo = "PSVR_AMPLA") =>
        Modalidade.Criar(codigo, "Ampla", "AMPLA", "RESIDUAL_DO_VO", null, null, null, null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, null)
            .Value!;

    [Fact(DisplayName = "Modalidade inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Modalidade?)null);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            new AtualizarModalidadeCommand(id, Descricao: "x", RegraDeAplicabilidade: Incondicional), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        Modalidade existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, Descricao: "Descrição nova", NaturezaLegal: "AMPLA", ComposicaoVagas: "RESIDUAL_DO_VO", RegraDeAplicabilidade: Incondicional);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("PSVR_AMPLA", "o código é imutável — não há campo para alterá-lo");
        existente.Descricao.Should().Be("Descrição nova");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Modalidade legal fixa recusa alteração estrutural antes de checar referências")]
    public async Task Handle_LegalFixa_RecusaSemChecarReferencias()
    {
        Modalidade existente = Existente("LB_PPI");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            RegraRemanejamento: "DESTINO_UNICO", RemanejamentoDestino: "AC", RegraDeAplicabilidade: OptouPelasCotas(false));

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _repository.DidNotReceive().CodigosVivosExistemAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência de remanejamento inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        Modalidade existente = Existente("SUP");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            RegraRemanejamento: "DESTINO_UNICO", RemanejamentoDestino: "XPTO", RegraDeAplicabilidade: OptouPelasCotas(false));

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Legal fixa com payload internamente incoerente ainda responde estrutura protegida, não a coerência")]
    public async Task Handle_LegalFixaComPayloadIncoerente_RespondeEstruturaProtegida()
    {
        Modalidade existente = Existente("LB_PPI");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        // Payload internamente incoerente (AMPLA não admite regra) E que também
        // tenta alterar a estrutura de uma modalidade legal fixa — a guarda tem
        // que vencer, não a coerência, mesmo com o pré-check do handler rodando
        // antes do fetch.
        var comando = new AtualizarModalidadeCommand(
            existente.Id, NaturezaLegal: "AMPLA", ComposicaoVagas: "DENTRO_DO_VR",
            RegraRemanejamento: "SEGUE_CASCATA", RegraDeAplicabilidade: Incondicional);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem buscar a modalidade por Id — validação vence 404")]
    public async Task Handle_CampoInvalido_RetornaErroSemBuscarPorId()
    {
        var comando = new AtualizarModalidadeCommand(Guid.CreateVersion7(), NaturezaLegal: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaInvalida);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato desativado que a regra vigente já citava continua aceito; citação nova é recusada")]
    public async Task Handle_FatoDesativado_SoRecusaCitacaoNova()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato), FatoDesativado("PCD"), FatoDesativado("QUILOMBOLA")]);
        Modalidade existente = Modalidade.Criar("PSIQ_PCD", "Ampla", "ACAO_AFIRMATIVA", "SUPLEMENTAR_AO_TOTAL", null,
            "DESTINO_UNICO", "AC", null, null, Predicado(Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao("PCD", true)), null, null).Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(true);

        AtualizarModalidadeCommand Comando(string fato) => new(
            existente.Id, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            RegraRemanejamento: "DESTINO_UNICO", RemanejamentoDestino: "AC",
            RegraDeAplicabilidade: [[Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao(fato, true)]]);

        (await AtualizarModalidadeCommandHandler.Handle(
                Comando("PCD"), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await AtualizarModalidadeCommandHandler.Handle(
                Comando("QUILOMBOLA"), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None))
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaFatoDesativado);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Regra malformada se acumula com as recusas dos demais campos, sem a de regra ausente")]
    public async Task Handle_RegraMalformada_AcumulaComOsDemaisCampos()
    {
        var comando = new AtualizarModalidadeCommand(
            Guid.CreateVersion7(), Descricao: new string('a', 301), NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[]]);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("descricao", ModalidadeErrorCodes.DescricaoTamanho),
            ("regraDeAplicabilidade", "ClausulaDnf.ClausulaVazia"),
        ]);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Legal fixa recusa a estrutura sem conferir a regra contra o catálogo")]
    public async Task Handle_LegalFixa_NaoConfereOCatalogo()
    {
        Modalidade existente = Existente("LB_PPI");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            new AtualizarModalidadeCommand(existente.Id, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[Condicao("PCD", true)]]),
            _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _fatoRepository.DidNotReceive().ListarTodosAsync(Arg.Any<CancellationToken>());
    }

    private static PredicadoDnf Predicado(params CondicaoDoPredicadoInput[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, CondicaoDnf.Criar(
            c.Fato, Operador.Igual, c.Valor).Value!))]).Value!;

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
