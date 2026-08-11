namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A existência e a atividade do código de uma regra legal pertencem à Configuração;
/// a factory de domínio apenas preserva o código que o handler validou.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCommandHandlerTests
{
    private const string TipoEtapaCodigo = "PROVA_OBJETIVA";

    private static ITipoEtapaReader TipoEtapaReaderAtivo() =>
        TipoEtapaReaderComResposta(new TipoEtapaView(Guid.CreateVersion7(), TipoEtapaCodigo, "Prova Objetiva", null));

    private static ITipoEtapaReader TipoEtapaReaderComResposta(TipoEtapaView? resposta)
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorCodigoAsync(TipoEtapaCodigo, Arg.Any<CancellationToken>()).Returns(resposta);
        return reader;
    }

    [Fact(DisplayName = "Handle aceita código de tipo ativo e persiste a obrigatoriedade")]
    public async Task Handle_TipoAtivo_Persiste()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_NOVO"), repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra => regra.TipoProcessoCodigo == "PS_NOVO"),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa código de tipo de processo inexistente ou desativado sem persistir regra")]
    public async Task Handle_TipoProcessoInexistenteOuInativo_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_DESATIVADO", Arg.Any<CancellationToken>())
            .Returns((TipoProcessoView?)null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_DESATIVADO"), repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle aceita o sentinela universal sem consultar os tipos de processo ativos")]
    public async Task Handle_TipoUniversal_NaoConsultaTiposAtivos()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra(ObrigatoriedadeLegal.TipoProcessoUniversal), repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    /// <summary>issue #1071 — decisão fechada: obrigatoriedade nova só pode usar código de tipo de etapa ativo.</summary>
    [Fact(DisplayName = "Handle recusa código de tipo de etapa inexistente ou desativado sem persistir regra")]
    public async Task Handle_TipoEtapaInexistenteOuInativo_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderComResposta(null);
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_NOVO"), repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// TipoEtapaCodigo é non-nullable só em compile-time: FluentValidation valida Predicado
    /// não-nulo, não os campos do subtipo polimórfico, e System.Text.Json não impõe NRT em
    /// runtime — um payload com <c>"tipoEtapaCodigo": null</c> desserializa sem erro.
    /// </summary>
    [Fact(DisplayName = "Handle recusa código de tipo de etapa nulo sem lançar")]
    public async Task Handle_TipoEtapaCodigoNulo_RecusaSemLancar()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));

        CriarObrigatoriedadeLegalCommand command = new(
            "PS_NOVO",
            CategoriaObrigatoriedade.Etapa,
            "REGRA_NOVA",
            new EtapaObrigatoria(null!),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O reader normaliza a busca (<c>Trim</c>), mas se o valor persistido não fosse
    /// normalizado a regra ficaria aceita e, ao mesmo tempo, permanentemente inatingível em
    /// AvaliadorConformidadeLegal — que compara por igualdade ordinal exata.
    /// </summary>
    [Fact(DisplayName = "Handle normaliza espaços do código de tipo de etapa antes de persistir")]
    public async Task Handle_TipoEtapaCodigoComEspacos_PersisteNormalizado()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = TipoEtapaReaderAtivo();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        CriarObrigatoriedadeLegalCommand command = new(
            "PS_NOVO",
            CategoriaObrigatoriedade.Etapa,
            "REGRA_NOVA",
            new EtapaObrigatoria($"  {TipoEtapaCodigo}  "),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra =>
                ((EtapaObrigatoria)regra.Predicado).TipoEtapaCodigo == TipoEtapaCodigo),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Predicados que não são EtapaObrigatoria não consultam o reader de tipos de etapa.</summary>
    [Fact(DisplayName = "Handle com predicado que não é EtapaObrigatoria não consulta os tipos de etapa ativos")]
    public async Task Handle_PredicadoNaoEEtapaObrigatoria_NaoConsultaTiposDeEtapa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ITipoEtapaReader tipoEtapaReader = Substitute.For<ITipoEtapaReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        CriarObrigatoriedadeLegalCommand command = new(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            "REGRA_NOVA",
            new ConcorrenciaDuplaObrigatoria(),
            "Descrição da regra.",
            "Lei de teste.",
            new DateOnly(2026, 1, 1),
            null,
            null,
            null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            command, repository, tipoReader, tipoEtapaReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoEtapaReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    private static CriarObrigatoriedadeLegalCommand NovaRegra(string tipoProcessoCodigo) => new(
        tipoProcessoCodigo,
        CategoriaObrigatoriedade.Etapa,
        "REGRA_NOVA",
        new EtapaObrigatoria(TipoEtapaCodigo),
        "Descrição da regra.",
        "Lei de teste.",
        new DateOnly(2026, 1, 1),
        null,
        null,
        null);
}
