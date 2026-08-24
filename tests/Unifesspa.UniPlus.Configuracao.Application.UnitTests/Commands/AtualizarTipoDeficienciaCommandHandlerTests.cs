namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarTipoDeficienciaCommandHandlerTests
{
    private const string Codigo = "DEFICIENCIA_VISUAL";
    private const string Descricao = "Deficiência relacionada à visão";

    private readonly ITipoDeficienciaRepository _repository = Substitute.For<ITipoDeficienciaRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static TipoDeficiencia TipoExistente(string codigo = Codigo, string nome = "Visual") =>
        TipoDeficiencia.Criar(codigo, nome, Descricao).Value!;

    private static AtualizarTipoDeficienciaCommand Comando(
        Guid id, string codigo = Codigo, string nome = "Visual") =>
        new(id, codigo, nome, Descricao);

    [Fact(DisplayName = "Tipo inexistente retorna NaoEncontrado (404)")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Guid id = Guid.CreateVersion7();
        _repository.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns((TipoDeficiencia?)null);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código que colide com outro tipo vivo retorna conflito (409)")]
    public async Task Handle_CodigoColidente_RetornaConflito()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        // O novo código "TEA" já pertence a outro tipo vivo.
        _repository.CodigoExisteEntreVivosAsync("TEA", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, codigo: "TEA"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.CodigoJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um código distinto e livre é aceito e persiste")]
    public async Task Handle_CodigoDistintoLivre_Aceita()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.CodigoExisteEntreVivosAsync("TEA", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, codigo: "TEA"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Codigo.Valor.Should().Be("TEA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Reenviar o próprio código não consulta a unicidade do código")]
    public async Task Handle_CodigoInalterado_NaoChecaUnicidadeDoCodigo()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar apenas o nome preserva o código e não consulta a unicidade do código")]
    public async Task Handle_SomenteNomeAlterado_PreservaCodigo()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.NomeExisteEntreVivosAsync("Baixa visão", existente.Id, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, nome: "Baixa visão"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.Nome.Should().Be("Baixa visão");
        existente.Codigo.Valor.Should().Be(Codigo);
        await _repository.DidNotReceive()
            .CodigoExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar para um nome que colide com outro tipo vivo retorna conflito (409)")]
    public async Task Handle_NomeColidente_RetornaConflito()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.NomeExisteEntreVivosAsync("Auditiva", existente.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id, nome: "Auditiva"), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeJaExiste);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Editar sem mudar o nome não consulta a unicidade do nome")]
    public async Task Handle_NomeInalterado_NaoChecaUnicidadeDoNome()
    {
        TipoDeficiencia existente = TipoExistente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            Comando(existente.Id), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await _repository.DidNotReceive()
            .NomeExisteEntreVivosAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Payload inválido em Id inexistente retorna a violação de validação, não NaoEncontrado")]
    public async Task Handle_PayloadInvalidoEIdInexistente_RetornaViolacaoDeValidacao()
    {
        Guid id = Guid.CreateVersion7();
        AtualizarTipoDeficienciaCommand comando = Comando(id, codigo: "", nome: "") with { Descricao = "" };

        Result resultado = await AtualizarTipoDeficienciaCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Error!.Code.Should().NotBe(TipoDeficienciaErrorCodes.NaoEncontrado);
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
