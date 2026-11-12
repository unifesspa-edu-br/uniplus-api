namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>A remoção de fase e de etapa iniciada: quais delas começaram e o que a versão publicada vigente ainda traz.</summary>
public sealed partial class UnidadeDaFaseNaVersaoVigenteTests
{
    [Fact(DisplayName = "A fase encerrada conta como iniciada e não sai do cronograma")]
    public async Task Iniciadas_FaseEncerrada_ContaENaoSai()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS fase encerrada", Abertura);
        await SucederAsync(processoId, DateTimeOffset.UtcNow, JanelaNoPassado);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.RecusaDaRemocao([])!.Code.Should().Be("FaseCronograma.FaseIniciadaNaoSaiDoCronograma");
    }

    [Fact(DisplayName = "A fase iniciada só pelo primeiro ato, com a janela ainda futura, não sai do cronograma")]
    public async Task Remocao_FaseIniciadaSoPeloAto_Recusa()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS remocao pelo ato", Abertura);
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.RecusaDaRemocao([])!.Code.Should().Be("FaseCronograma.FaseIniciadaNaoSaiDoCronograma");
    }

    [Fact(DisplayName = "A fase que ainda não começou pode sair do cronograma")]
    public async Task Remocao_FaseNaoIniciada_NaoRecusa()
    {
        (Guid processoId, _, _) = await PublicadoAsync("PS remocao de fase por comecar", Abertura);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.RecusaDaRemocao([]).Should().BeNull();
    }

    [Fact(DisplayName = "A remoção é conferida contra a versão vigente, e não contra a de vigência futura")]
    public async Task Remocao_VersaoDeVigenciaFutura_NaoLivraAFase()
    {
        (Guid processoId, _, _) = await PublicadoAsync("PS remocao e versao futura");
        await SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), f => f["faseCanonicaOrigemId"] = Guid.CreateVersion7());

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.RecusaDaRemocao([])!.Code.Should().Be("FaseCronograma.FaseIniciadaNaoSaiDoCronograma");
    }

    [Fact(DisplayName = "A fase iniciada que a versão vigente já não traz não conta como removida")]
    public async Task Remocao_FaseJaForaDaVersaoVigente_NaoRecusa()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS fase fora da vigente");
        await SucederAsync(processoId, DateTimeOffset.UtcNow, f =>
        {
            f["faseCanonicaOrigemId"] = Guid.CreateVersion7();
            AdiaAJanela(f);
        });

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.RecusaDaRemocao([]).Should().BeNull();
    }

    [Fact(DisplayName = "A janela de uma etapa inicia a fase que a contém, ainda que a janela da fase seja futura")]
    public async Task Iniciadas_ContaPelaJanelaDaEtapa()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela da etapa", Abertura);
        await SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00+00:00");

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "A geração do envio ao INEP numa etapa inicia a fase que a contém")]
    public async Task Iniciadas_ContaPeloEnvioAoInep()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS envio ao INEP", Abertura);
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await PublicadoAsync("PS sem envio ao INEP", Abertura);
        await SemearEnvioAoInepAsync(processoId, etapa.Id);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);
        FasesIniciadas deOutroProcesso = await IniciadasAsync(outroProcessoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa com a janela aberta está iniciada e não sai do cronograma; a irmã sem janela nem registro sai")]
    public async Task Etapas_JanelaAberta_IniciaSoAEtapa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await PublicadoAsync("PS janela aberta da etapa", Abertura);
        Guid irma = Guid.CreateVersion7();
        await SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AbreAJanelaDaEtapaEAcrescentaAIrma(envelope, irma));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeTrue();
        iniciadas.ContemEtapa(irma).Should().BeFalse("a abertura de uma etapa não inicia as outras da fase");
        iniciadas.RecusaDaRemocaoDaEtapa([])!.Code.Should().Be("FaseCronograma.EtapaIniciadaNaoSaiDoCronograma");
    }

    [Fact(DisplayName = "A etapa com a avaliação lançada, sem janela, está iniciada, e a irmã sem registro não")]
    public async Task Etapas_AvaliacaoLancada_IniciaSoAEtapa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await PublicadoAsync("PS avaliacao da etapa sem janela", Abertura);
        Guid irma = Guid.CreateVersion7();
        await SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AcrescentaEtapaIrma(envelope, irma));
        await SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeTrue();
        iniciadas.ContemEtapa(irma).Should().BeFalse();
    }

    [Fact(DisplayName = "A etapa encerrada e a etapa com ato próprio estão iniciadas")]
    public async Task Etapas_EncerradaEComAto_Iniciadas()
    {
        (Guid encerradaId, _, EtapaProcesso encerrada) = await PublicadoAsync("PS etapa encerrada", Abertura);
        await SucederNoEnvelopeAsync(encerradaId, DateTimeOffset.UtcNow, envelope => envelope["etapas"]!.AsArray()[0]!["fim"] = "2020-01-31T00:00:00+00:00");
        (Guid comAtoId, _, EtapaProcesso comAto) = await PublicadoAsync("PS etapa com ato", Abertura);
        await SemearAtoAsync(comAtoId, IdentidadeDoItemDoEdital.DaEtapa(comAto, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        (await IniciadasAsync(encerradaId, DateTimeOffset.UtcNow)).ContemEtapa(encerrada.Id).Should().BeTrue();
        (await IniciadasAsync(comAtoId, DateTimeOffset.UtcNow)).ContemEtapa(comAto.Id).Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa que ainda não começou pode sair do cronograma")]
    public async Task Etapas_NaoIniciada_NaoRecusa()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await PublicadoAsync("PS etapa por comecar", Abertura);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.ContemEtapa(etapa.Id).Should().BeFalse();
        iniciadas.RecusaDaRemocaoDaEtapa([]).Should().BeNull();
    }

    [Fact(DisplayName = "A janela de uma versão de vigência futura não inicia a etapa")]
    public async Task Etapas_JanelaDeVersaoFutura_NaoConta()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await PublicadoAsync("PS etapa de versao futura", Abertura);
        await SucederNoEnvelopeAsync(
            processoId, DateTimeOffset.UtcNow.AddDays(30), envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00+00:00");

        (await IniciadasAsync(processoId, DateTimeOffset.UtcNow)).ContemEtapa(etapa.Id).Should().BeFalse();
    }

    [Fact(DisplayName = "A janela de uma versão sucedida no mesmo instante em que passou a vigir não inicia a etapa")]
    public async Task Etapas_JanelaDeVersaoQueNuncaVigorou_NaoConta()
    {
        (Guid processoId, _, EtapaProcesso etapa) = await PublicadoAsync("PS versao que nunca vigorou", Abertura);
        DateTimeOffset vigencia = DateTimeOffset.UtcNow.AddMinutes(-5);
        await SucederNoEnvelopeAsync(processoId, vigencia, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00+00:00");
        await SucederNoEnvelopeAsync(processoId, vigencia, envelope => envelope["etapas"]!.AsArray()[0]!["inicio"] = null);

        (await IniciadasAsync(processoId, DateTimeOffset.UtcNow)).ContemEtapa(etapa.Id).Should().BeFalse();
    }

    private static void AcrescentaEtapaIrma(JsonObject envelope, Guid id)
    {
        JsonArray etapas = envelope["etapas"]!.AsArray();
        JsonObject irma = JsonNode.Parse(etapas[0]!.ToJsonString())!.AsObject();
        irma["id"] = id;
        irma["nome"] = "Análise documental";
        irma["inicio"] = null;
        irma["fim"] = null;
        etapas.Add(irma);
    }

    private static void AbreAJanelaDaEtapaEAcrescentaAIrma(JsonObject envelope, Guid irma)
    {
        AcrescentaEtapaIrma(envelope, irma);
        envelope["etapas"]!.AsArray()[0]!["inicio"] = "2020-01-01T00:00:00+00:00";
    }

    private async Task SemearEnvioAoInepAsync(Guid processoId, Guid etapaId)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        semeadura.GeracoesDeEnvioAoInep.Add(GeracaoDeEnvioAoInep.Registrar(
            processoId, etapaId, "2026", "INEP_ENEM_ENVIO", "v1", new string('d', 64), "sub-admin", DateTimeOffset.UtcNow, 0));
        await semeadura.SaveChangesAsync();
    }
}
