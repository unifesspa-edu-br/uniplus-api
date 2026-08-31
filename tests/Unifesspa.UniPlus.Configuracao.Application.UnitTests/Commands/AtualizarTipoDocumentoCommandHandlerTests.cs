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
    private readonly ICategoriaDocumentoRepository _categoriaRepository = Substitute.For<ICategoriaDocumentoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    public AtualizarTipoDocumentoCommandHandlerTests()
    {
        // O caso comum é a categoria existir; o teste que exercita a ausência
        // sobrescreve esta configuração.
        _categoriaRepository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private static TipoDocumento TipoExistente(string codigo = "CIN", string categoria = "IDENTIFICACAO") =>
        TipoDocumento.Criar(codigo, "Carteira de Identidade Nacional", null, categoria, null, null, null).Value!;

    private static AtualizarTipoDocumentoCommand Comando(Guid id, string codigo = "CIN", string categoria = "IDENTIFICACAO") =>
        new(id, codigo, "Carteira de Identidade Nacional", categoria, "Documento unificado", "pdf", 5, null);

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDocumento?)null);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(id), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Trocar para categoria ausente do cadastro é recusado sem persistir")]
    public async Task Handle_NovaCategoriaInexistente_Recusa()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _categoriaRepository.CodigoExisteEntreVivosAsync("INEXISTENTE", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, categoria: "INEXISTENTE"),
            _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaNaoEncontrada);
        resultado.Errors[0].Field.Should().Be("categoria");
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo cuja categoria saiu do cadastro continua editável nos demais campos")]
    public async Task Handle_CategoriaOrfaInalterada_NaoConsultaOCadastroEAtualiza()
    {
        // A categoria pode ser removida ou renomeada sem tocar nos tipos que a
        // referenciavam. Exigir categoria viva a cada edição travaria a manutenção
        // desses tipos — nem corrigir o nome deles seria possível.
        TipoDocumento existente = TipoExistente("CIN", categoria: "CATEGORIA_REMOVIDA");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _categoriaRepository.CodigoExisteEntreVivosAsync("CATEGORIA_REMOVIDA", null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, categoria: "CATEGORIA_REMOVIDA"),
            _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _categoriaRepository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código que colide com outro tipo vivo retorna conflito (409)")]
    public async Task Handle_CodigoColidente_RetornaConflito()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        // O novo código "RG" já pertence a outro tipo vivo.
        _repository.CodigoExisteEntreVivosAsync("RG", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "RG"), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

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
            Comando(existente.Id, codigo: "CIN_NOVO"), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("CIN_NOVO");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Antes do validator removido, um payload mal formado nunca chegava a
    /// ObterPorIdAsync — validação sempre vencia sobre "não encontrado". Sem o
    /// validator, o handler precisa preservar essa prioridade explicitamente.
    /// </summary>
    [Fact(DisplayName = "Id inexistente com Nome vazio devolve a violação de campo, não NaoEncontrado, sem consultar o repositório")]
    public async Task Handle_IdInexistenteComNomeVazio_RetornaViolacaoDeCampoSemConsultarRepositorio()
    {
        AtualizarTipoDocumentoCommand comando = Comando(Guid.CreateVersion7()) with { Nome = "" };

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Antes: a checagem de unicidade rodava antes de o agregado revalidar os
    /// demais campos, então um código colidente mascarava qualquer outra
    /// violação de campo atrás de um CodigoJaExiste.
    /// </summary>
    [Fact(DisplayName = "Editar para código colidente com Nome vazio reporta a violação de Nome, não CodigoJaExiste")]
    public async Task Handle_CodigoColidenteComNomeVazio_ReportaViolacaoDeCampoAntesDeConsultarUnicidade()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        AtualizarTipoDocumentoCommand comando = Comando(existente.Id, codigo: "RG") with { Nome = "" };

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            comando, _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NomeObrigatorio);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o código não consulta a unicidade")]
    public async Task Handle_CodigoInalterado_NaoChecaUnicidade()
    {
        TipoDocumento existente = TipoExistente("CIN");
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoDocumentoCommandHandler.Handle(
            Comando(existente.Id, codigo: "CIN"), _repository, _categoriaRepository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
