namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarCategoriaDocumentoCommandHandlerTests
{
    private const string CodigoVivoConstraint = "ix_categoria_documento_codigo_vivo";

    private readonly ICategoriaDocumentoRepository _repository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarCategoriaDocumentoCommand ComandoValido() =>
        new("DOCUMENTO_PROCESSUAL", "Documento processual", "Instrui o processo administrativo", 30);

    [Fact(DisplayName = "Código livre cria a categoria, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync("DOCUMENTO_PROCESSUAL", null, Arg.Any<CancellationToken>())
            .Returns(false);
        CategoriaDocumento? adicionada = null;
        await _repository.AdicionarAsync(
            Arg.Do<CategoriaDocumento>(c => adicionada = c), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        adicionada.Should().NotBeNull();
        adicionada!.Ordem.Should().Be(30, "a ordem de exibição informada é persistida junto com o cadastro");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivas retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync("DOCUMENTO_PROCESSUAL", null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<CategoriaDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código fora do formato propaga o erro de domínio sem consultar unicidade nem persistir")]
    public async Task Handle_CodigoFormatoInvalido_RetornaErroSemConsultarUnicidadeNemPersistir()
    {
        CriarCategoriaDocumentoCommand comando = ComandoValido() with { Codigo = "01" };

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoFormatoInvalido);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código ausente e nome ausente no mesmo payload acumulam as duas violações")]
    public async Task Handle_CodigoENomeAusentes_AcumulaAsDuasViolacoes()
    {
        CriarCategoriaDocumentoCommand comando = ComandoValido() with { Codigo = "", Nome = "" };

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[1].Field.Should().Be("nome");
    }

    [Fact(DisplayName = "Corrida com outro INSERT vira CodigoJaExiste e descarta o rastreamento")]
    public async Task Handle_CorridaNoIndiceUnico_TraduzEDescartaRastreamento()
    {
        // A pré-checagem passa (nenhuma categoria viva com o código) e a colisão só
        // aparece no INSERT: sem a tradução, o SaveChangesAsync automático do Wolverine
        // repetiria a inserção fora do catch e o 409 pretendido viraria 500.
        _repository.CodigoExisteEntreVivosAsync("DOCUMENTO_PROCESSUAL", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", CodigoVivoConstraint)));

        Result<Guid> resultado = await CriarCategoriaDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Violação de outra constraint não é engolida — propaga")]
    public async Task Handle_OutraViolacaoDeConstraint_Propaga()
    {
        _repository.CodigoExisteEntreVivosAsync("DOCUMENTO_PROCESSUAL", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", "outra_constraint_qualquer")));

        Func<Task> act = () => CriarCategoriaDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
