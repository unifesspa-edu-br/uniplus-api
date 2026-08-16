namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoBancaCommandHandlerTests
{
    private readonly ITipoBancaRepository _repository = Substitute.For<ITipoBancaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoBancaCommand Comando() =>
        new("BANCA_ENTREVISTA", Nome: "Banca de entrevista");

    [Fact(DisplayName = "Código livre cria a banca, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("BANCA_ENTREVISTA", null, Arg.Any<CancellationToken>()).Returns(false);

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
        _repository.CodigoExisteEntreVivosAsync("BANCA_ENTREVISTA", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoBanca>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do conjunto canônico propaga o erro sem consultar unicidade nem persistir")]
    public async Task Handle_ForaDoCanonico_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        var comando = new CriarTipoBancaCommand("BANCA_LOGISTICA", Nome: "x");

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do canônico e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoForaDoCanonicoENomeAusente_AcumulaAsDuasViolacoes()
    {
        var comando = new CriarTipoBancaCommand("BANCA_LOGISTICA", Nome: "");

        Result<Guid> resultado = await CriarTipoBancaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }
}
