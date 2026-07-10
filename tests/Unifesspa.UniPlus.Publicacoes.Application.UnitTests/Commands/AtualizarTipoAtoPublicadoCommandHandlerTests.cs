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
public sealed class AtualizarTipoAtoPublicadoCommandHandlerTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly ITipoAtoPublicadoRepository _repository = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();

    [Fact(DisplayName = "Recusa quando o tipo de ato não existe")]
    public async Task Handle_NaoEncontrado_Falha()
    {
        _repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(Guid.NewGuid()), _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Não consulta sobreposição quando código e janela não mudam")]
    public async Task Handle_SoMudaNome_NaoConsultaSobreposicao()
    {
        TipoAtoPublicado existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { Nome = "Outro nome" }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();

        // A janela não mudou, logo nenhuma sobreposição pode ter surgido: a consulta
        // seria um round-trip inútil por update.
        await _repository.DidNotReceive().ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Consulta sobreposição quando o código ou a janela mudam")]
    [InlineData("EDITAL_RETIFICACAO", 2026, 1, 1, null)]
    [InlineData("EDITAL_ABERTURA", 2026, 3, 1, null)]
    [InlineData("EDITAL_ABERTURA", 2026, 1, 1, 2027)]
    public async Task Handle_MudaJanelaOuCodigo_ConsultaSobreposicao(
        string codigo, int ano, int mes, int dia, int? anoFim)
    {
        TipoAtoPublicado existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        DateOnly? fim = anoFim is { } a ? new DateOnly(a, 1, 1) : null;
        AtualizarTipoAtoPublicadoCommand comando = Comando(existente.Id) with
        {
            Codigo = codigo,
            VigenciaInicio = new DateOnly(ano, mes, dia),
            VigenciaFim = fim,
        };

        await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            comando, _repository, _unitOfWork, CancellationToken.None);

        // A própria versão é excluída da checagem — senão ela colidiria consigo mesma.
        await _repository.Received(1).ExisteSobreposicaoDeVigenciaAsync(
            codigo, comando.VigenciaInicio, fim, existente.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa quando a nova janela intercepta outra versão viva")]
    public async Task Handle_ComSobreposicao_Falha()
    {
        TipoAtoPublicado existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        _repository.ExisteSobreposicaoDeVigenciaAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { VigenciaInicio = Inicio.AddMonths(2) },
            _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.VigenciaSobreposta);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Propaga o erro do agregado e não commita")]
    public async Task Handle_ComPayloadInvalido_NaoCommita()
    {
        TipoAtoPublicado existente = Existente();
        _repository.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarTipoAtoPublicadoCommandHandler.Handle(
            Comando(existente.Id) with { Nome = "E" }, _repository, _unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.NomeTamanho);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static TipoAtoPublicado Existente() =>
        TipoAtoPublicado.Criar(
            "EDITAL_ABERTURA", "Edital de abertura",
            congelaConfiguracao: true, unicoPorObjeto: true, efeitoIrreversivel: false,
            vigenciaInicio: Inicio, vigenciaFim: null, baseLegal: null).Value!;

    private static AtualizarTipoAtoPublicadoCommand Comando(Guid id) =>
        new(
            Id: id,
            Codigo: "EDITAL_ABERTURA",
            Nome: "Edital de abertura",
            CongelaConfiguracao: true,
            UnicoPorObjeto: true,
            EfeitoIrreversivel: false,
            VigenciaInicio: Inicio);
}
