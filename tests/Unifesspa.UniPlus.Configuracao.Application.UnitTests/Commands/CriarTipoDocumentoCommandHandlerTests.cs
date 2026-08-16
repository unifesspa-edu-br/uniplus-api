namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoDocumentoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _repository = Substitute.For<ITipoDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarTipoDocumentoCommand ComandoValido() =>
        new("LAUDO_MEDICO", "Laudo médico", "SAUDE", "Documento de saúde", "pdf,jpg", 10, null);

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("LAUDO_MEDICO", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("LAUDO_MEDICO", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Categoria inválida propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_CategoriaInvalida_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { Categoria = "FINANCEIRO" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaInvalida);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente e categoria inválida no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeAusenteECategoriaInvalida_AcumulaAsDuasViolacoes()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { Nome = "", Categoria = "FINANCEIRO" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("categoria");
    }
}
