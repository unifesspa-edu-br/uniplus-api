namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoDocumentoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _repository = Substitute.For<ITipoDocumentoRepository>();
    private readonly ICategoriaDocumentoRepository _categoriaRepository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    public CriarTipoDocumentoCommandHandlerTests()
    {
        // O caso comum é a categoria existir; os testes que exercitam a ausência
        // sobrescrevem esta configuração.
        _categoriaRepository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static CriarTipoDocumentoCommand ComandoValido() =>
        new("LAUDO_MEDICO", "Laudo médico", "SAUDE", "Documento de saúde", "pdf,jpg", 10, null);

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código já existente entre vivos retorna conflito (CodigoJaExiste) sem persistir")]
    public async Task Handle_CodigoDuplicado_RetornaConflito()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido(), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Categoria fora do formato propaga o erro de domínio sem consultar o cadastro nem persistir")]
    public async Task Handle_CategoriaForaDoFormato_RetornaErroSemConsultarCadastroNemPersistir()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { Categoria = "financeiro" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaFormatoInvalido);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<CodigoTipoDocumento>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Categoria bem formada mas inexistente entre as vivas é recusada sem persistir")]
    public async Task Handle_CategoriaInexistenteNoCadastro_RecusaSemPersistir()
    {
        _categoriaRepository.CodigoExisteEntreVivosAsync("INEXISTENTE", null, Arg.Any<CancellationToken>())
            .Returns(false);
        CriarTipoDocumentoCommand comando = ComandoValido() with { Categoria = "INEXISTENTE" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaNaoEncontrada);
        resultado.Errors[0].Field.Should().Be("categoria");
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome ausente e categoria mal formada no mesmo payload acumulam as duas violações")]
    public async Task Handle_NomeAusenteECategoriaMalFormada_AcumulaAsDuasViolacoes()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { Nome = "", Categoria = "financeiro" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("categoria");
    }

    /// Constrói o value object do código para casar com a assinatura do repositório,
    /// que recebe `CodigoTipoDocumento` — não a string crua.
    private static CodigoTipoDocumento Codigo(string valor) => CodigoTipoDocumento.Criar(valor).Value!;

}
