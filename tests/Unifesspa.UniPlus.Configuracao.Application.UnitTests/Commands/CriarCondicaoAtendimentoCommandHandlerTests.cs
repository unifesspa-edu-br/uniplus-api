namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCondicaoAtendimentoCommandHandlerTests
{
    private readonly ICondicaoAtendimentoRepository _repository = Substitute.For<ICondicaoAtendimentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarCondicaoAtendimentoCommand ComandoValido() =>
        new("DISLEXIA", "Dislexia", "Transtorno específico de aprendizagem");

    [Fact(DisplayName = "Código livre cria a condição, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("DISLEXIA", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarCondicaoAtendimentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<CondicaoAtendimentoEspecializado>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("DISLEXIA", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarCondicaoAtendimentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<CondicaoAtendimentoEspecializado>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do formato propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_CodigoFormatoInvalido_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarCondicaoAtendimentoCommand comando = ComandoValido() with { Codigo = "dislexia" };

        Result<Guid> resultado = await CriarCondicaoAtendimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        CriarCondicaoAtendimentoCommand comando = ComandoValido() with { Codigo = "", Nome = "" };

        Result<Guid> resultado = await CriarCondicaoAtendimentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }
}
