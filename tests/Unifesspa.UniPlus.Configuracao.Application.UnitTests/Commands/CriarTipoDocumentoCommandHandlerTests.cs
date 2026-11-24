namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarTipoDocumentoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _repository = Substitute.For<ITipoDocumentoRepository>();
    private readonly ICategoriaDocumentoRepository _categoriaRepository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly GravacaoDoTipoDocumento _gravacao;

    public CriarTipoDocumentoCommandHandlerTests()
    {
        _gravacao = new GravacaoDoTipoDocumento(_repository, _categoriaRepository, _unitOfWork);

        // O caso comum é a categoria existir; os testes que exercitam a ausência
        // sobrescrevem esta configuração.
        _categoriaRepository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static CriarTipoDocumentoCommand ComandoValido() =>
        new("LAUDO_MEDICO", "Laudo médico", "SAUDE", "Documento de saúde", "pdf,jpg", 10, null,
            "SENSIVEL", "Comprovação do atendimento especializado.", "TUTELA_DA_SAUDE");

    [Fact(DisplayName = "Código livre cria o tipo, persiste e retorna o Id")]
    public async Task Handle_CodigoLivre_CriaEPersiste()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido(), _gravacao, CancellationToken.None);

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
            ComandoValido(), _gravacao, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Categoria fora do formato propaga o erro de domínio sem consultar o cadastro nem persistir")]
    public async Task Handle_CategoriaForaDoFormato_RetornaErroSemConsultarCadastroNemPersistir()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { Categoria = "financeiro" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(comando, _gravacao, CancellationToken.None);

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

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(comando, _gravacao, CancellationToken.None);

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

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(comando, _gravacao, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("categoria");
    }

    /// Constrói o value object do código para casar com a assinatura do repositório,
    /// que recebe `CodigoTipoDocumento` — não a string crua.
    private static CodigoTipoDocumento Codigo(string valor) => CodigoTipoDocumento.Criar(valor).Value!;

    [Fact(DisplayName = "Comando sem a proteção é recusado como proteção obrigatória, sem consultar nem persistir")]
    public async Task Handle_SemProtecao_RecusaSemPersistir()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with
        {
            ClassificacaoProtecao = null,
            FinalidadeTratamento = null,
            HipoteseLegal = null,
        };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(comando, _gravacao, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Should().AllSatisfy(e => e.Error.Code.Should().Be(TipoDocumentoErrorCodes.ProtecaoObrigatoria));
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Token de classificação fora do vocabulário é recusado como inválido, não como ausente")]
    public async Task Handle_ClassificacaoForaDoVocabulario_RecusaComoInvalida()
    {
        CriarTipoDocumentoCommand comando = ComandoValido() with { ClassificacaoProtecao = "ALTISSIMA" };

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(comando, _gravacao, CancellationToken.None);

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Error.Code.Should().Be(TipoDocumentoErrorCodes.ClassificacaoProtecaoInvalida);
    }

    [Fact(DisplayName = "A corrida no índice único do código devolve CodigoJaExiste e descarta o rastreamento, para o save automático não estourar fora do catch")]
    public async Task Handle_CorridaNoIndiceUnico_TraduzEDescartaRastreamento()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>()).Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", RestricoesDoBanco.TipoDeDocumentoCodigoVivo)));

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(ComandoValido(), _gravacao, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "A violação de outro índice único não é traduzida: propaga sem descartar")]
    public async Task Handle_ViolacaoDeOutroIndice_Propaga()
    {
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>()).Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética de teste", PostgresExceptionFactory.Create("23505", "ix_outro")));

        Func<Task> acao = () => CriarTipoDocumentoCommandHandler.Handle(ComandoValido(), _gravacao, CancellationToken.None);

        await acao.Should().ThrowAsync<DbUpdateException>();
        _unitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Categoria inexistente é recusada antes de conferir o código, mesmo com o código já existente")]
    public async Task Handle_CategoriaInexistenteECodigoExistente_RecusaACategoriaSemConsultarOCodigo()
    {
        _categoriaRepository.CodigoExisteEntreVivosAsync("INEXISTENTE", null, Arg.Any<CancellationToken>()).Returns(false);
        _repository.CodigoExisteEntreVivosAsync(Codigo("LAUDO_MEDICO"), null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarTipoDocumentoCommandHandler.Handle(
            ComandoValido() with { Categoria = "INEXISTENTE" }, _gravacao, CancellationToken.None);

        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaNaoEncontrada);
        await _repository.DidNotReceive().CodigoExisteEntreVivosAsync(Arg.Any<CodigoTipoDocumento>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
