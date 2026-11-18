namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarBaseLegalBonusRegionalCommandHandlerTests
{
    private readonly IBaseLegalBonusRegionalRepository _repository = Substitute.For<IBaseLegalBonusRegionalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

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

    [Fact(DisplayName = "Município nulo na lista é recusado com violação de campo, sem lançar exceção")]
    public async Task Handle_MunicipioNulo_RecusaSemLancar()
    {
        CriarBaseLegalBonusRegionalCommand comando = ComandoValido() with
        {
            Municipios = [null!],
        };

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
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

    [Fact(DisplayName = "Código já usado por uma base legal viva é recusado com CodigoJaExiste, sem persistir")]
    public async Task Handle_CodigoJaExiste_Recusa()
    {
        _repository.CodigoExisteAsync(Arg.Is<CodigoBaseLegalBonusRegional>(c => c.Valor == "PORTARIA_UNIFESSPA_NO_2514_2023"), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<BaseLegalBonusRegional>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida decidida pelo índice único do código vira CodigoJaExiste e descarta o rastreamento")]
    public async Task Handle_CorridaNoIndice_TraduzEDescarta()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.BaseLegalBonusRegionalCodigoVivo)));

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(BaseLegalBonusRegionalErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint propaga, sem virar CodigoJaExiste")]
    public async Task Handle_OutraConstraint_Propaga()
    {
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("outra", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => CriarBaseLegalBonusRegionalCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        await acao.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "O mesmo texto em outro tipo de instrumento é outra base legal e é aceito")]
    public async Task Handle_MesmoTextoEmOutroTipo_Aceita()
    {
        _repository.CodigoExisteAsync(Arg.Is<CodigoBaseLegalBonusRegional>(c => c.Valor == "LEI_12_2020"), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarBaseLegalBonusRegionalCommandHandler.Handle(
            ComandoValido() with { TipoInstrumento = "DECRETO", Identificacao = "12/2020" },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }
}
