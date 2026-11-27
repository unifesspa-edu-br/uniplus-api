namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static CenarioDeFasesDoProcessoPublicado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaUnidadeDaFaseNaVersaoVigente;

/// <summary>A remoção de fase e de etapa iniciada: quais delas começaram e o que a versão publicada vigente ainda traz.</summary>
public sealed class UnidadeDaFaseNaVersaoVigenteRemocaoDeFaseEEtapaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;
    private readonly ApoioDaUnidadeDaFaseNaVersaoVigente _apoio;

    public UnidadeDaFaseNaVersaoVigenteRemocaoDeFaseEEtapaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);
        _apoio = new ApoioDaUnidadeDaFaseNaVersaoVigente(fixture);
    }

    [Fact(DisplayName = "A fase encerrada conta como iniciada e não sai do cronograma")]
    public async Task Iniciadas_FaseEncerrada_ContaENaoSai()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS fase encerrada", Abertura);
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, JanelaNoPassado);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.CitacoesDasFasesRemovidas([]).Should().ContainSingle().Which.Should().Be(fase.Citacao);
    }

    [Fact(DisplayName = "A fase iniciada só pelo primeiro ato, com a janela ainda futura, não sai do cronograma")]
    public async Task Remocao_FaseIniciadaSoPeloAto_Recusa()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS remocao pelo ato", Abertura);
        await _cenario.SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.CitacoesDasFasesRemovidas([]).Should().ContainSingle().Which.Should().Be(fase.Citacao);
    }

    [Fact(DisplayName = "A fase que ainda não começou pode sair do cronograma")]
    public async Task Remocao_FaseNaoIniciada_NaoRecusa()
    {
        (Guid processoId, _, _) = await _cenario.PublicadoAsync("PS remocao de fase por comecar", Abertura);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.CitacoesDasFasesRemovidas([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A remoção é conferida contra a versão vigente, e não contra a de vigência futura")]
    public async Task Remocao_VersaoDeVigenciaFutura_NaoLivraAFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS remocao e versao futura");
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), f => f["tipoFaseOrigemId"] = Guid.CreateVersion7());

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.CitacoesDasFasesRemovidas([]).Should().ContainSingle().Which.Should().Be(fase.Citacao);
    }

    [Fact(DisplayName = "A fase iniciada que a versão vigente já não traz não conta como removida")]
    public async Task Remocao_FaseJaForaDaVersaoVigente_NaoRecusa()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS fase fora da vigente");
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, f =>
        {
            f["tipoFaseOrigemId"] = Guid.CreateVersion7();
            AdiaAJanela(f);
        });

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.CitacoesDasFasesRemovidas([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela de uma etapa inicia a fase que a contém, ainda que a janela da fase seja futura")]
    public async Task Iniciadas_ContaPelaJanelaDaEtapa()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS janela da etapa", Abertura);
        await _apoio.SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = InstanteCanonico(2020, 1, 1));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "A geração do envio ao INEP numa etapa inicia a fase que a contém")]
    public async Task Iniciadas_ContaPeloEnvioAoInep()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS envio ao INEP", Abertura);
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await _cenario.PublicadoAsync("PS sem envio ao INEP", Abertura);
        await _apoio.SemearEnvioAoInepAsync(processoId, etapa.Id);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);
        FasesIniciadas deOutroProcesso = await _cenario.IniciadasAsync(outroProcessoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa com a janela aberta está iniciada e não sai do cronograma; a irmã sem janela nem registro sai")]
    public async Task Etapas_JanelaAberta_IniciaSoAEtapa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS janela aberta da etapa", Abertura);
        Guid irma = Guid.CreateVersion7();
        await _apoio.SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AbreAJanelaDaEtapaEAcrescentaAIrma(envelope, irma));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeTrue();
        iniciadas.ContemEtapa(irma).Should().BeFalse("a abertura de uma etapa não inicia as outras da fase");
        iniciadas.EtapasRemovidas([]).Should().ContainSingle().Which.Id.Should().Be(etapa.Id);
    }

    [Fact(DisplayName = "A etapa com a avaliação lançada, sem janela, está iniciada, e a irmã sem registro não")]
    public async Task Etapas_AvaliacaoLancada_IniciaSoAEtapa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS avaliacao da etapa sem janela", Abertura);
        Guid irma = Guid.CreateVersion7();
        await _apoio.SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AcrescentaEtapaIrma(envelope, irma));
        await _apoio.SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeTrue();
        iniciadas.ContemEtapa(irma).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa encerrada e a etapa com ato próprio estão iniciadas")]
    public async Task Etapas_EncerradaEComAto_Iniciadas()
    {
        (Guid encerradaId, _, EtapaProcesso encerrada) = await _cenario.PublicadoAsync("PS etapa encerrada", Abertura);
        await _apoio.SucederNoEnvelopeAsync(encerradaId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["fim"] = InstanteCanonico(2020, 1, 31));
        (Guid comAtoId, _, EtapaProcesso comAto) = await _cenario.PublicadoAsync("PS etapa com ato", Abertura);
        await _cenario.SemearAtoAsync(comAtoId, IdentidadeDoItemDoEdital.DaEtapa(comAto, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)));

        (await _cenario.IniciadasAsync(encerradaId, DateTimeOffset.UtcNow)).ContemEtapa(encerrada.Id).Should().BeTrue();
        (await _cenario.IniciadasAsync(comAtoId, DateTimeOffset.UtcNow)).ContemEtapa(comAto.Id).Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa que ainda não começou pode sair do cronograma")]
    public async Task Etapas_NaoIniciada_NaoRecusa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS etapa por comecar", Abertura);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeFalse();
        iniciadas.EtapasRemovidas([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A janela de uma versão de vigência futura não inicia a etapa")]
    public async Task Etapas_JanelaDeVersaoFutura_NaoConta()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS etapa de versao futura", Abertura);
        await _apoio.SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow.AddDays(30), envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = InstanteCanonico(2020, 1, 1));

        (await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow)).ContemEtapa(etapa.Id).Should().BeFalse();
    }

    [Fact(DisplayName = "A janela de uma versão sucedida no mesmo instante em que passou a vigir não inicia a etapa")]
    public async Task Etapas_JanelaDeVersaoQueNuncaVigorou_NaoConta()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS versao que nunca vigorou", Abertura);
        DateTimeOffset vigencia = DateTimeOffset.UtcNow.AddMinutes(-5);
        await _apoio.SucederNoEnvelopeAsync(processoId, vigencia, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = InstanteCanonico(2020, 1, 1));
        await _apoio.SucederNoEnvelopeAsync(processoId, vigencia, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = null);

        (await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow)).ContemEtapa(etapa.Id).Should().BeFalse();
    }
}
