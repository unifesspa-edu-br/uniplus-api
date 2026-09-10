namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarBaseLegalBonusRegionalCommandHandlerTests
{
    private readonly IBaseLegalBonusRegionalRepository _repository = Substitute.For<IBaseLegalBonusRegionalRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarBaseLegalBonusRegionalCommand ComandoValido() =>
        new(
            "PORTARIA",
            "Portaria Unifesspa nº 2514/2023",
            "Institui inclusão regional",
            [new CriarBaseLegalBonusRegionalMunicipioCommand("1504208", "Marabá", "PA")]);

    [Fact(DisplayName = "Payload válido cria a entidade, persiste e retorna o Id")]
    public async Task Handle_PayloadValido_CriaEPersiste()
    {
        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<BaseLegalBonusRegional>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload sem nenhum município é recusado sem persistir")]
    public async Task Handle_SemMunicipio_RecusaSemPersistir()
    {
        CriarBaseLegalBonusRegionalCommand comando = ComandoValido() with { Municipios = [] };

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.SemMunicipios);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<BaseLegalBonusRegional>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Município com código IBGE fora do formato é recusado sem persistir")]
    public async Task Handle_MunicipioComCodigoIbgeInvalido_RecusaSemPersistir()
    {
        CriarBaseLegalBonusRegionalCommand comando = ComandoValido() with
        {
            Municipios = [new CriarBaseLegalBonusRegionalMunicipioCommand("123", "Marabá", "PA")],
        };

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.MunicipioInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de instrumento fora do vocabulário fechado é recusado sem persistir")]
    public async Task Handle_TipoInstrumentoInvalido_RecusaSemPersistir()
    {
        CriarBaseLegalBonusRegionalCommand comando = ComandoValido() with { TipoInstrumento = "MEDIDA_PROVISORIA" };

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
