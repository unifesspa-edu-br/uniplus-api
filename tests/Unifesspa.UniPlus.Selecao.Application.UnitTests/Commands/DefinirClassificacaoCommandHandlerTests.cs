namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

public sealed class DefinirClassificacaoCommandHandlerTests
{
    [Fact(DisplayName = "Handle com processo inexistente retorna ProcessoSeletivo.NaoEncontrado")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        CenarioDaClassificacao cenario = SemProcesso();
        DefinirClassificacaoCommand command = new(
            Guid.CreateVersion7(), "X", "v1", null, null, null, "Y", "v1", [], false, null, null);

        Result result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Handle com FORMULA_MEDIA_PONDERADA + arredondamento resolve e persiste")]
    public async Task Handle_MediaPonderadaComArredondamento_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas();

        Result result = await cenario.DefinirAsync(ComandoDeMediaPonderada(processo.Id));

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao.Should().NotBeNull();
        processo.Classificacao!.RegraArredondamento.Should().NotBeNull();
        await cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle repassa a escala da nota final para Criar — chega até a configuração persistida")]
    public async Task Handle_PropagaAEscalaDaNotaFinal()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas();

        Result result = await cenario.DefinirAsync(ComandoDeMediaPonderada(processo.Id) with { EscalaNotaFinal = 1000m });

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao!.EscalaNotaFinal.Should().Be(1000m);
    }

    [Theory(DisplayName = "Handle repassa BaseadoEmEnem para Criar — chega até a configuração persistida (#850)")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_PropagaBaseadoEmEnem(bool baseadoEmEnem)
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).ComRegrasBasicas().ComResolucaoDosPesos();

        // Classificação baseada em ENEM com cálculo local exige a resolução de Pesos por Área.
        string? resolucao = baseadoEmEnem ? ResolucaoDePesos : null;
        DefinirClassificacaoCommand command = ComandoDeMediaPonderada(processo.Id) with { BaseadoEmEnem = baseadoEmEnem, ResolucaoPesoAreaEnem = resolucao };

        Result result = await cenario.DefinirAsync(command);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.BaseadoEmEnem.Should().Be(baseadoEmEnem,
            "o rótulo TipoProcesso (PSIQ, não-ENEM) não pode sobrepor o dado declarado no command");
    }

    [Fact(DisplayName = "Handle com CLASSIFICACAO_IMPORTADA sem arredondamento resolve e persiste")]
    public async Task Handle_Importada_SemArredondamento_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("SiSU 2026", TiposDeProcessoDeTeste.SiSU);
        CenarioDaClassificacao cenario = Com(processo)
            .ComRegra(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), TipoRegra.RegraCalculo)
            .ComRegra(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), TipoRegra.RegraOrdemAlocacao);

        Result result = await cenario.DefinirAsync(
            ComandoEnemLocal(processo.Id, null, baseadoEmEnem: false, RegraDeCalculoDaNota.ClassificacaoImportada.Codigo()));

        result.IsSuccess.Should().BeTrue();
        processo.Classificacao!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Handle com regra de cálculo inexistente recusa")]
    public async Task Handle_RegraCalculoNaoEncontrada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).SemNenhumaRegraNoCatalogo();
        DefinirClassificacaoCommand command = new(
            processo.Id, "INEXISTENTE", "v1", null, null, null, RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", [], false, null, null);

        Result result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraNaoEncontrada");
    }

    [Fact(DisplayName = "Handle com regra de cálculo de tipo inválido recusa")]
    public async Task Handle_RegraCalculoTipoInvalido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = Com(processo).ComRegra(RegraDeCalculoDaNota.MediaPonderada.Codigo(), TipoRegra.RegraArredondamento);
        DefinirClassificacaoCommand command = new(
            processo.Id, RegraDeCalculoDaNota.MediaPonderada.Codigo(), "v1", null, null, null,
            RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", [], false, null, null);

        Result result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ConfiguracaoClassificacao.RegraTipoInvalido");
    }
}
