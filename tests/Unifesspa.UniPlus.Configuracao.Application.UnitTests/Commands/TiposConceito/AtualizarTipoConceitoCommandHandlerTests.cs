namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands.TiposConceito;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtualizarTipoConceitoCommandHandlerTests
{
    private readonly ITipoConceitoRepository _repository = Substitute.For<ITipoConceitoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private Task<Result> Atualizar(AtualizarTipoConceitoCommand comando) =>
        AtualizarTipoConceitoCommandHandler.Handle(comando, _repository, _unitOfWork, CancellationToken.None);

    private TipoConceito TipoExistente(TipoConceito tipo)
    {
        _repository.ObterPorIdAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        return tipo;
    }

    [Fact(DisplayName = "Atualiza o tipo numérico e persiste")]
    public async Task Handle_Numerico_AtualizaEPersiste()
    {
        TipoConceito tipo = TipoExistente(TiposConceitoDeTeste.Numerico());

        Result resultado = await Atualizar(TiposConceitoDeTeste.AtualizarNumerico(tipo.Id));

        resultado.IsSuccess.Should().BeTrue();
        (tipo.Nome, tipo.EscalaMaxima, tipo.CasasDecimais).Should().Be(("Nota da entrevista revisada", (decimal?)100m, (int?)1));
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Substitui os valores do tipo categórico em bloco, na ordem do corpo")]
    public async Task Handle_Categorico_SubstituiOsValoresEmBloco()
    {
        TipoConceito tipo = TipoExistente(TiposConceitoDeTeste.Categorico());

        await Atualizar(TiposConceitoDeTeste.AtualizarCategorico(tipo.Id));

        tipo.Valores.Select(v => v.Codigo).Should().Equal("INDEFERIDO", "DEFERIDO", "EM_ANALISE");
    }

    [Fact(DisplayName = "Tipo inexistente retorna não encontrado sem persistir")]
    public async Task Handle_Inexistente_RetornaNaoEncontrado()
    {
        Result resultado = await Atualizar(TiposConceitoDeTeste.AtualizarNumerico(Guid.NewGuid()));

        resultado.Error!.Code.Should().Be(TipoConceitoErrorCodes.NaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Corpo inválido vence o não encontrado e não consulta o repositório")]
    public async Task Handle_CorpoInvalido_NaoConsultaORepositorio()
    {
        Result resultado = await Atualizar(TiposConceitoDeTeste.AtualizarNumerico(Guid.NewGuid()) with { Nome = "", Natureza = "x" });

        resultado.Errors.Select(e => e.Field).Should().Equal("nome", "natureza");
        await _repository.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Permite trocar a natureza do tipo")]
    public async Task Handle_TrocaDeNatureza_Aplica()
    {
        TipoConceito tipo = TipoExistente(TiposConceitoDeTeste.Numerico());

        await Atualizar(TiposConceitoDeTeste.AtualizarCategorico(tipo.Id));

        (tipo.Natureza, tipo.EscalaMaxima, tipo.Valores.Count).Should().Be((NaturezaConceito.Categorico, (decimal?)null, 3));
    }
}
