namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoEtapaCommandHandlerTests
{
    private readonly ITipoEtapaRepository _repository = Substitute.For<ITipoEtapaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoEtapaCommand ComandoValido() =>
        new("TIPO_ETAPA_TESTE", "Tipo de etapa de teste", null);

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteAsync("TIPO_ETAPA_TESTE", Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarTipoEtapaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoEtapa>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteAsync("TIPO_ETAPA_TESTE", Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoEtapaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoEtapa>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_NomeAusente_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoEtapaCommand comando = ComandoValido() with { Nome = "" };

        Result<Guid> resultado = await CriarTipoEtapaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().CodigoExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        CriarTipoEtapaCommand comando = ComandoValido() with { Codigo = "", Nome = "" };

        Result<Guid> resultado = await CriarTipoEtapaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }
}
