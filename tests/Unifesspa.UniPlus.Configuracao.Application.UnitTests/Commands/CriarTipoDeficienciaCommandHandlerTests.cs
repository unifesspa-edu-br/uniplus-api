namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoDeficienciaCommandHandlerTests
{
    private const string Codigo = "DEFICIENCIA_VISUAL";

    private readonly ITipoDeficienciaRepository _repository = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoDeficienciaCommand ComandoValido() =>
        new(Codigo, "Visual", "Deficiência relacionada à visão");

    [Fact(DisplayName = "Código e nome livres criam o tipo, persistem e retornam o Id")]
    public async Task Handle_CodigoENomeLivres_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.NomeExisteEntreVivosAsync("Visual", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoDeficiencia>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo, null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDeficiencia>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome já existente entre vivos retorna conflito (NomeJaExiste) sem persistir")]
    public async Task Handle_NomeDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo, null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.NomeExisteEntreVivosAsync("Visual", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDeficiencia>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do formato propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_CodigoInvalido_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoDeficienciaCommand comando = ComandoValido() with { Codigo = "deficiencia_visual" };

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoFormatoInvalido);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome inválido propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_NomeInvalido_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoDeficienciaCommand comando = ComandoValido() with { Nome = "A" };

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
        await _repository.DidNotReceive()
            .NomeExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do formato e descrição ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoInvalidoEDescricaoAusente_AcumulaAsDuasViolacoes()
    {
        CriarTipoDeficienciaCommand comando = ComandoValido() with { Codigo = "1_DEFICIENCIA", Descricao = "" };

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("descricao");
    }

    [Fact(DisplayName = "Código, nome e descrição ausentes no mesmo payload acumulam as três violações")]
    public async Task Handle_TodosOsCamposAusentes_AcumulaAsTresViolacoes()
    {
        CriarTipoDeficienciaCommand comando = ComandoValido() with { Codigo = "", Nome = "", Descricao = "" };

        Result<Guid> resultado = await CriarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[2].Field.Should().Be("descricao");
    }
}
