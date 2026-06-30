namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarTipoDocumentoCommandHandlerTests
{
    private readonly ITipoDocumentoRepository _repository = Substitute.For<ITipoDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoDocumento TipoExistente(string codigo = "CIN") =>
        TipoDocumento.Criar(codigo, "Carteira de Identidade Nacional", null, "IDENTIFICACAO", null, null, null).Value!;

    private static AtualizarTipoDocumentoCommand Comando(Guid id, string codigo = "CIN") =>
        new(id, codigo, "Carteira de Identidade Nacional", "IDENTIFICACAO", "Documento unificado", "pdf", 5, null);

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDocumento?)null);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código que colide com outro tipo vivo retorna conflito (409)")]
    public async Task Handle_CodigoColidente_RetornaConflito()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        // O novo código "RG" já pertence a outro tipo vivo.
        _repository.CodigoExisteEntreVivosAsync("RG", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "RG"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código distinto e livre é aceito e persiste")]
    public async Task Handle_CodigoDistintoLivre_Aceita()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigoExisteEntreVivosAsync("CIN_NOVO", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "CIN_NOVO"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Should().Be("CIN_NOVO");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o código não consulta a unicidade")]
    public async Task Handle_CodigoInalterado_NaoChecaUnicidade()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "CIN"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
