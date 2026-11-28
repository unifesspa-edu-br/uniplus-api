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
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class AtualizarModalidadeCommandHandlerTests
{
    private readonly IModalidadeRepository _repository = Substitute.For<IModalidadeRepository>();
    private static readonly IReadOnlyList<IReadOnlyList<CondicaoDoPredicadoInput>> Incondicional = [];

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
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
        Modalidade.Criar(codigo, new DeclaracaoDaModalidade(
                ModalidadesDeTeste.Nome, "Ampla", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS", null, null, null, null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, null))
            .Value!;

    private Task<Result> Atualizar(AtualizarModalidadeCommand comando) =>
        AtualizarModalidadeCommandHandler.Handle(
            comando, _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

    private Modalidade ExistenteNoRepositorio(string codigo = "PSVR_AMPLA")
    {
        Modalidade existente = Existente(codigo);
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        return existente;
    }

    [Fact(DisplayName = "Modalidade inexistente retorna NaoEncontrada (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((Modalidade?)null);

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            new AtualizarModalidadeCommand(id, Nome: ModalidadesDeTeste.Nome, Descricao: "x", RegraDeAplicabilidade: Incondicional), _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização válida persiste e o código permanece imutável")]
    public async Task Handle_Valido_PersisteCodigoImutavel()
    {
        Modalidade existente = ExistenteNoRepositorio();

        var comando = new AtualizarModalidadeCommand(
            existente.Id, Nome: ModalidadesDeTeste.Nome, Descricao: "Descrição nova", NaturezaLegal: "AMPLA", ComposicaoVagas: "RESTANTE_DAS_VAGAS_OFERTADAS", RegraDeAplicabilidade: Incondicional);

        Result resultado = await Atualizar(comando);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("PSVR_AMPLA", "o código é imutável — não há campo para alterá-lo");
        existente.Descricao.Should().Be("Descrição nova");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome já usado por outra modalidade viva retorna conflito, descarta a alteração e não persiste")]
    public async Task Handle_NomeDeOutraModalidade_RetornaConflitoEDescarta()
    {
        Modalidade existente = ExistenteNoRepositorio();
        _repository.NomeExisteEntreVivosAsync("Nome de outra modalidade", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await Atualizar(ComandoComONome(existente.Id, "Nome de outra modalidade"));

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A própria modalidade pode manter o próprio nome")]
    public async Task Handle_MesmoNomeDaPropriaModalidade_Persiste()
    {
        Modalidade existente = ExistenteNoRepositorio();

        Result resultado = await Atualizar(ComandoComONome(existente.Id, ModalidadesDeTeste.Nome));

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).NomeExisteEntreVivosAsync(ModalidadesDeTeste.Nome, existente.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O nome já usado é recusado antes da referência inexistente, e a alteração é descartada")]
    public async Task Handle_NomeDeOutraModalidadeEReferenciaInexistente_RecusaONomePrimeiro()
    {
        Modalidade existente = ExistenteNoRepositorio("SUP");
        _repository.NomeExisteEntreVivosAsync(Arg.Any<string>(), existente.Id, Arg.Any<CancellationToken>()).Returns(true);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(false);
        var comando = new AtualizarModalidadeCommand(
            existente.Id, Nome: "Nome de outra modalidade", NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            Remanejamento: new("DESTINO_UNICO", Destino: "XPTO"), RegraDeAplicabilidade: OptouPelasCotas(false));

        Result resultado = await Atualizar(comando);

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Atualização sem nome é recusada sem buscar a modalidade")]
    public async Task Handle_SemNome_RecusaSemBuscarPorId()
    {
        Result resultado = await Atualizar(ComandoComONome(Guid.CreateVersion7(), null));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadeErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O nome é editável numa modalidade legal fixa, que mantém a estrutura")]
    public async Task Handle_LegalFixaComNomeNovoEEstruturaIgual_Persiste()
    {
        Modalidade existente = ExistenteNoRepositorio("AC");

        Result resultado = await Atualizar(ComandoComONome(existente.Id, "Nome novo da ampla concorrência"));

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Nome novo da ampla concorrência");
    }

    [Fact(DisplayName = "Corrida pelo índice do nome na atualização vira NomeJaExiste e descarta o que não foi salvo")]
    public async Task Handle_CorridaPeloIndiceDoNome_RetornaNomeJaExisteEDescarta()
    {
        Modalidade existente = ExistenteNoRepositorio();
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException(
            "violação sintética de teste", PostgresExceptionFactory.Create("23505", RestricoesDoBanco.ModalidadeNomeAtivos)));

        Result resultado = await Atualizar(ComandoComONome(existente.Id, "Nome novo"));

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NomeJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outro índice único na atualização propaga sem descartar")]
    public async Task Handle_CorridaPorOutroIndice_Propaga()
    {
        Modalidade existente = ExistenteNoRepositorio();
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new DbUpdateException(
            "violação sintética de teste", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => Atualizar(ComandoComONome(existente.Id, "Nome novo"));

        await acao.Should().ThrowAsync<DbUpdateException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    private static AtualizarModalidadeCommand ComandoComONome(Guid id, string? nome) => new(
        id, Nome: nome, NaturezaLegal: "AMPLA", ComposicaoVagas: "RESTANTE_DAS_VAGAS_OFERTADAS", RegraDeAplicabilidade: Incondicional);

    [Fact(DisplayName = "Modalidade legal fixa recusa alteração estrutural antes de checar referências")]
    public async Task Handle_LegalFixa_RecusaSemChecarReferencias()
    {
        Modalidade existente = ExistenteNoRepositorio("LB_PPI");

        var comando = new AtualizarModalidadeCommand(
            existente.Id, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            Remanejamento: new("DESTINO_UNICO", Destino: "AC"), RegraDeAplicabilidade: OptouPelasCotas(false));

        Result resultado = await Atualizar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _repository.DidNotReceive().CodigosVivosExistemAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Referência de remanejamento inexistente entre vivos retorna 422 sem persistir")]
    public async Task Handle_ReferenciaInexistente_Retorna422()
    {
        Modalidade existente = ExistenteNoRepositorio("SUP");
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var comando = new AtualizarModalidadeCommand(
            existente.Id, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
            Remanejamento: new("DESTINO_UNICO", Destino: "XPTO"), RegraDeAplicabilidade: OptouPelasCotas(false));

        Result resultado = await Atualizar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O payload é internamente incoerente (AMPLA não admite regra) E também tenta alterar a estrutura de uma
    /// modalidade legal fixa: a guarda tem que vencer, não a coerência, mesmo com o pré-check do handler rodando
    /// antes do fetch.
    /// </summary>
    [Fact(DisplayName = "Legal fixa com payload internamente incoerente ainda responde estrutura protegida, não a coerência")]
    public async Task Handle_LegalFixaComPayloadIncoerente_RespondeEstruturaProtegida()
    {
        Modalidade existente = ExistenteNoRepositorio("LB_PPI");
        var comando = new AtualizarModalidadeCommand(
            existente.Id, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA", ComposicaoVagas: "DENTRO_DA_RESERVA_LEGAL",
            Remanejamento: new("SEGUE_ORDEM_DE_REMANEJAMENTO"), RegraDeAplicabilidade: Incondicional);

        Result resultado = await Atualizar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem buscar a modalidade por Id — validação vence 404")]
    public async Task Handle_CampoInvalido_RetornaErroSemBuscarPorId()
    {
        var comando = new AtualizarModalidadeCommand(Guid.CreateVersion7(), Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "XPTO", RegraDeAplicabilidade: Incondicional);

        Result resultado = await Atualizar(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaInvalida);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato desativado que a regra vigente já citava continua aceito; citação nova é recusada")]
    public async Task Handle_FatoDesativado_SoRecusaCitacaoNova()
    {
        _fatoRepository.ListarTodosAsync(Arg.Any<CancellationToken>()).Returns([Fato(OpcaoPelasCotasDaLei.Fato), FatoDesativado("PCD"), FatoDesativado("QUILOMBOLA")]);
        Modalidade existente = ExistenteQueJaCitaOFatoPcd();

        Result jaCitado = await Atualizar(ComandoQueCita(existente.Id, "PCD"));
        Result citacaoNova = await Atualizar(ComandoQueCita(existente.Id, "QUILOMBOLA"));

        jaCitado.IsSuccess.Should().BeTrue();
        citacaoNova.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ModalidadeErrorCodes.RegraCitaFatoDesativado);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private Modalidade ExistenteQueJaCitaOFatoPcd()
    {
        Modalidade existente = Modalidade.Criar("PSIQ_PCD", new DeclaracaoDaModalidade(
            ModalidadesDeTeste.Nome, "Ampla", "ACAO_AFIRMATIVA", "SUPLEMENTAR_AO_TOTAL", null, "DESTINO_UNICO", "AC", null, null,
            Predicado(Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao("PCD", true)), null, null)).Value!;
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigosVivosExistemAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(true);
        return existente;
    }

    private static AtualizarModalidadeCommand ComandoQueCita(Guid id, string fato) => new(
        id, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "ACAO_AFIRMATIVA", ComposicaoVagas: "SUPLEMENTAR_AO_TOTAL",
        Remanejamento: new("DESTINO_UNICO", Destino: "AC"),
        RegraDeAplicabilidade: [[Condicao(OpcaoPelasCotasDaLei.Fato, false), Condicao(fato, true)]]);

    [Fact(DisplayName = "Regra malformada se acumula com as recusas dos demais campos, sem a de regra ausente")]
    public async Task Handle_RegraMalformada_AcumulaComOsDemaisCampos()
    {
        var comando = new AtualizarModalidadeCommand(
            Guid.CreateVersion7(), Nome: ModalidadesDeTeste.Nome, Descricao: new string('a', 301), NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[]]);

        Result resultado = await Atualizar(comando);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("descricao", ModalidadeErrorCodes.DescricaoTamanho),
            ("regraDeAplicabilidade[0]", "ClausulaDnf.ClausulaVazia"),
        ]);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Legal fixa recusa a estrutura sem conferir a regra contra o catálogo")]
    public async Task Handle_LegalFixa_NaoConfereOCatalogo()
    {
        Modalidade existente = ExistenteNoRepositorio("LB_PPI");

        Result resultado = await AtualizarModalidadeCommandHandler.Handle(
            new AtualizarModalidadeCommand(existente.Id, Nome: ModalidadesDeTeste.Nome, NaturezaLegal: "AMPLA", RegraDeAplicabilidade: [[Condicao("PCD", true)]]),
            _repository, _fatoRepository, _precedenciaRepository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel);
        await _fatoRepository.DidNotReceive().ListarTodosAsync(Arg.Any<CancellationToken>());
    }

    private static PredicadoDnf Predicado(params CondicaoDoPredicadoInput[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, CondicaoDnf.Criar(
            c.Fato, Operador.Igual, c.Valor).Value!))]).Value!;

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
