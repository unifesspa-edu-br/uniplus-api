namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class CriarTipoBancaCommandHandlerTests
{
    private readonly ITipoBancaRepository _repository = Substitute.For<ITipoBancaRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static CriarTipoBancaCommand Comando() =>
        new("BANCA_ENTREVISTA", Nome: "Banca de entrevista", Papel: "AVALIA_O_MERITO", TiposDeDecisaoJulgados: ["NOTA_DA_ETAPA"]);

    [Fact(DisplayName = "Código livre cria a banca, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(TipoDeBanca.Entrevista, null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoBanca>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(TipoDeBanca.Entrevista, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoBanca>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do vocabulário propaga o erro sem consultar unicidade nem persistir")]
    public async Task Handle_ForaDoCanonico_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        var comando = new CriarTipoBancaCommand("BANCA_LOGISTICA", Nome: "x", Papel: "IMPORTA_CANDIDATOS");

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<TipoDeBanca>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do vocabulário e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoForaDoCanonicoENomeAusente_AcumulaAsDuasViolacoes()
    {
        var comando = new CriarTipoBancaCommand("BANCA_LOGISTICA", Nome: "", Papel: "IMPORTA_CANDIDATOS");

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }

    [Fact(DisplayName = "Papel, marcas e tipos de decisão do comando chegam ao tipo criado e persistido")]
    public async Task Handle_ComAtuacao_PersisteAAtuacao()
    {
        var comando = new CriarTipoBancaCommand(
            "BANCA_HETEROIDENTIFICACAO", Nome: "Banca de heteroidentificação", Papel: "AVALIA_O_MERITO",
            ExigeDuasOuMaisBancas: true, EncerraRecursoNaPrimeiraInstancia: true, TiposDeDecisaoJulgados: ["HETEROIDENTIFICACAO"]);

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AdicionarAsync(
            Arg.Is<TipoBanca>(b => b.Atuacao.Papel == PapelDaBanca.AvaliaOMerito
                && b.Atuacao.ExigeDuasOuMaisBancas
                && b.Atuacao.EncerraRecursoNaPrimeiraInstancia
                && b.Atuacao.TiposDeDecisaoJulgados.SequenceEqual(new[] { TipoMotivoDeDecisao.Heteroidentificacao })),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Papel ausente, papel que não avalia o mérito com tipo de decisão e tipo repetido acumulam as violações sem consultar unicidade")]
    public async Task Handle_AtuacaoInvalida_AcumulaSemConsultarUnicidade()
    {
        var comando = new CriarTipoBancaCommand(
            "BANCA_ENTREVISTA", Nome: "Banca de entrevista", Papel: "IMPORTA_CANDIDATOS",
            TiposDeDecisaoJulgados: ["NOTA_DA_ETAPA", "NOTA_DA_ETAPA"]);

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("tiposDeDecisaoJulgados[1]", TipoBancaErrorCodes.TipoDeDecisaoRepetido),
            ("tiposDeDecisaoJulgados", TipoBancaErrorCodes.TiposDeDecisaoIncoerentesComOPapel));
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<TipoDeBanca>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
