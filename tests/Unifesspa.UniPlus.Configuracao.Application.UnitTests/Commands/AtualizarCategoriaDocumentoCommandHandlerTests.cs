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

public sealed class AtualizarCategoriaDocumentoCommandHandlerTests
{
    private const string CodigoVivoConstraint = "ix_categoria_documento_codigo_vivo";

    private readonly ICategoriaDocumentoRepository _repository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CategoriaDocumento Existente(string codigo = "RENDA", int ordem = 10) =>
        CategoriaDocumento.Criar(codigo, "Renda", null, ordem).Value!;

    [Fact(DisplayName = "Atualização válida aplica os campos, inclusive a ordem, e persiste")]
    public async Task Handle_Valido_AtualizaEPersiste()
    {
        CategoriaDocumento categoria = Existente();
        _repository.ObterPorIdAsync(categoria.Id, Arg.Any<CancellationToken>()).Returns(categoria);

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(categoria.Id, "RENDA", "Renda familiar", "comprovação de renda", 45),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        categoria.Nome.Should().Be("Renda familiar");
        categoria.Descricao.Should().Be("comprovação de renda");
        categoria.Ordem.Should().Be(45);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Id inexistente retorna NaoEncontrada sem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrada()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CategoriaDocumento?)null);

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(Guid.CreateVersion7(), "RENDA", "Renda", null, 0),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.NaoEncontrada);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload inválido é recusado antes de buscar o registro — validação vence 404")]
    public async Task Handle_PayloadInvalido_NaoBuscaRegistro()
    {
        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(Guid.CreateVersion7(), "01", "", null, -1),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().Equal(["codigo", "nome", "ordem"]);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Novo código já usado por outra categoria viva retorna CodigoJaExiste")]
    public async Task Handle_NovoCodigoOcupado_RetornaConflito()
    {
        CategoriaDocumento categoria = Existente();
        _repository.ObterPorIdAsync(categoria.Id, Arg.Any<CancellationToken>()).Returns(categoria);
        _repository.CodigoExisteEntreVivosAsync("ESCOLARIDADE", categoria.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(categoria.Id, "ESCOLARIDADE", "Escolaridade", null, 0),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código inalterado não dispara consulta de unicidade")]
    public async Task Handle_CodigoInalterado_NaoConsultaUnicidade()
    {
        CategoriaDocumento categoria = Existente();
        _repository.ObterPorIdAsync(categoria.Id, Arg.Any<CancellationToken>()).Returns(categoria);

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(categoria.Id, "RENDA", "Renda", null, 0),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corrida no UPDATE vira CodigoJaExiste e descarta o rastreamento")]
    public async Task Handle_CorridaNoIndiceUnico_TraduzEDescartaRastreamento()
    {
        CategoriaDocumento categoria = Existente();
        _repository.ObterPorIdAsync(categoria.Id, Arg.Any<CancellationToken>()).Returns(categoria);
        _repository.CodigoExisteEntreVivosAsync("ESCOLARIDADE", categoria.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException(
                "violação sintética de teste",
                PostgresExceptionFactory.Create("23505", CodigoVivoConstraint)));

        Result resultado = await AtualizarCategoriaDocumentoCommandHandler.Handle(
            new AtualizarCategoriaDocumentoCommand(categoria.Id, "ESCOLARIDADE", "Escolaridade", null, 0),
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoJaExiste);
        _unitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
