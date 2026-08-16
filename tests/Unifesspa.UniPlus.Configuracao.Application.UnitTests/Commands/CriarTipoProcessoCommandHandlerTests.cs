namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoProcessoCommandHandlerTests
{
    private readonly ITipoProcessoRepository _repository = Substitute.For<ITipoProcessoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoProcessoCommand ComandoValido() =>
        new("TIPO_PROCESSO_TESTE", "Tipo de processo de teste", null);

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteAsync("TIPO_PROCESSO_TESTE", Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarTipoProcessoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoProcesso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteAsync("TIPO_PROCESSO_TESTE", Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoProcessoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoProcesso>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código reservado propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_CodigoReservado_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoProcessoCommand comando = ComandoValido() with { Codigo = "*" };

        Result<Guid> resultado = await CriarTipoProcessoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.CodigoReservado);
        await _repository.DidNotReceive().CodigoExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        CriarTipoProcessoCommand comando = ComandoValido() with { Codigo = "", Nome = "" };

        Result<Guid> resultado = await CriarTipoProcessoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }
}
