namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarPesoAreaEnemCommandHandlerTests
{
    private readonly IPesoAreaEnemRepository _repository = Substitute.For<IPesoAreaEnemRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static CriarPesoAreaEnemCommand ComandoValido() =>
        new(PesoAreaEnemDados.Resolucao, GrupoCurso.Tecnologica, PesoAreaEnemDados.AreasDoPayload(), PesoAreaEnemDados.BaseLegal);

    [Fact(DisplayName = "Cria a linha de pesos com as cinco áreas, persiste e retorna o Id")]
    public async Task Handle_ParLivre_CriaEPersiste()
    {
        _repository.ParExisteEntreVivosAsync(PesoAreaEnemDados.Resolucao, GrupoCurso.Tecnologica, null, Arg.Any<CancellationToken>())
            .Returns(false);
        PesoAreaEnem? adicionado = null;
        await _repository.AdicionarAsync(Arg.Do<PesoAreaEnem>(p => adicionado = p), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        adicionado!.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso)).Should().Equal(
            PesoAreaEnem.Areas.Zip([2.00m, 1.50m, 2.50m, 2.50m, 1.50m], (area, peso) => (area.Codigo, area.Rotulo, peso)));
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par (resolução, grupo) já existente entre vivos retorna conflito (ParJaExiste)")]
    public async Task Handle_ParDuplicado_RetornaConflito()
    {
        _repository.ParExisteEntreVivosAsync(PesoAreaEnemDados.Resolucao, GrupoCurso.Tecnologica, null, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            ComandoValido(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.ParJaExiste);
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<PesoAreaEnem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Peso negativo propaga o erro no campo da área, sem persistir")]
    public async Task Handle_PesoNegativo_RetornaErroSemPersistir()
    {
        CriarPesoAreaEnemCommand comando = ComandoValido() with
        {
            Areas = PesoAreaEnemDados.AreasDoPayloadCom(4, new(PesoAreaEnem.CodigoMatematica, -1.00m)),
        };

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Field.Should().Be("areas[4].peso");
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.PesoNegativo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item nulo na lista de áreas vira erro do campo daquele índice, sem exceção")]
    public async Task Handle_ItemNulo_ViraErroDoCampo()
    {
        List<PesoAreaEnemAreaCommand> areas = PesoAreaEnemDados.AreasDoPayload();
        areas[1] = null!;
        CriarPesoAreaEnemCommand comando = ComandoValido() with { Areas = areas };

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        FieldError erro = resultado.Errors.Single(e =>
            e.Field == "areas[1].codigo" && e.Error.Code == PesoAreaEnemErrorCodes.AreaForaDoDominio);
        erro.Error.Message.Should().StartWith("Informe o código da área", "sem código não há valor a citar na mensagem");
    }

    [Fact(DisplayName = "Campo inválido no payload propaga o erro sem consultar unicidade nem persistir — validação vence I/O")]
    public async Task Handle_CampoInvalido_RetornaErroSemConsultarBancoNemPersistir()
    {
        CriarPesoAreaEnemCommand comando = ComandoValido() with { GrupoCurso = "Engenharias" };

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PesoAreaEnemErrorCodes.GrupoCursoInvalido);
        await _repository.DidNotReceive().ParExisteEntreVivosAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<PesoAreaEnem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Grupo inválido e área faltando acumulam as duas violações")]
    public async Task Handle_GrupoInvalidoEAreaFaltando_AcumulaAsDuasViolacoes()
    {
        List<PesoAreaEnemAreaCommand> areas = PesoAreaEnemDados.AreasDoPayload();
        areas.RemoveAt(0);
        CriarPesoAreaEnemCommand comando = ComandoValido() with { GrupoCurso = "Engenharias", Areas = areas };

        Result<Guid> resultado = await CriarPesoAreaEnemCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            [PesoAreaEnemErrorCodes.GrupoCursoInvalido, PesoAreaEnemErrorCodes.AreaFaltando]);
    }
}
