namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CriarTipoAtoPublicadoCommandHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();

    [Fact(DisplayName = "Cria, persiste e commita quando não há sobreposição")]
    public async Task Handle_SemSobreposicao_Cria()
    {
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            "EDITAL_ABERTURA", Inicio, null, null, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AdicionarAsync(Arg.Any<TipoAtoPublicado>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa quando a janela intercepta uma versão viva do mesmo código")]
    public async Task Handle_ComSobreposicao_Falha()
    {
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Result<Guid> resultado = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando(), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);

        // Nada é escrito nem commitado: a recusa acontece antes de tocar o agregado.
        await _repository.DidNotReceive().AdicionarAsync(Arg.Any<TipoAtoPublicado>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propaga o erro do agregado quando o código é inválido")]
    public async Task Handle_ComCodigoInvalido_PropagaErroDeDominio()
    {
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Result<Guid> resultado = await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando() with { Codigo = "edital abertura" }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoFormato);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A checagem de sobreposição recebe exatamente a janela do comando")]
    public async Task Handle_ConsultaComAJanelaInformada()
    {
        DateOnly fim = Inicio.AddYears(1);
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await CriarTipoAtoPublicadoCommandHandler.Handle(
            Comando() with { VigenciaFim = fim }, _repository, _unitOfWork, CancellationToken.None);

        await _repository.Received(1).ExisteSobreposicaoDeVigenciaAsync(
            "EDITAL_ABERTURA", Inicio, fim, null, Arg.Any<CancellationToken>());
    }

    private static CriarTipoAtoPublicadoCommand Comando() =>
        new(
            Codigo: "EDITAL_ABERTURA",
            Nome: "Edital de abertura",
            CongelaConfiguracao: true,
            UnicoPorObjeto: true,
            EfeitoIrreversivel: false,
            VigenciaInicio: Inicio);
}
