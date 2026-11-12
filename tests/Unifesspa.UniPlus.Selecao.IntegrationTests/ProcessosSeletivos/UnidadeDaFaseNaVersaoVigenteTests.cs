namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A unidade da fase e a das fases iniciadas, lidas do envelope congelado da versão vigente num Postgres real.
/// </summary>
public sealed class UnidadeDaFaseNaVersaoVigenteTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public UnidadeDaFaseNaVersaoVigenteTests(ProcessoSeletivoDbFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A fase publicada resolve a unidade congelada na versão vigente")]
    public async Task DaFase_ResolveAUnidadeDaVersaoVigente()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS unidade da fase");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        UnidadeDaFaseNaVersaoVigente leitor = new(context, TimeProvider.System);

        UnidadeDaFase? daFase = await leitor.DaFaseAsync(processoId, fase.Id);
        UnidadeDaFase? daEtapa = await leitor.DaEtapaAsync(processoId, etapa.Id);

        daFase.Should().Be(new UnidadeDaFase(fase.Id, UnidadesDeTeste.Ceps.OrigemId));
        daEtapa.Should().Be(daFase, "a etapa segue a unidade da fase que a contém");
    }

    [Fact(DisplayName = "Fase ou etapa inexistente, de outro processo ou de processo sem versão não resolvem unidade")]
    public async Task NaoResolve_FaseOuEtapaQueNaoSeEncontra()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS fase inexistente");
        (Guid outroProcessoId, _, _) = await PublicadoAsync("PS outro processo");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        UnidadeDaFaseNaVersaoVigente leitor = new(context, TimeProvider.System);

        (await leitor.DaFaseAsync(processoId, Guid.CreateVersion7())).Should().BeNull();
        (await leitor.DaEtapaAsync(processoId, Guid.CreateVersion7())).Should().BeNull();
        (await leitor.DaFaseAsync(outroProcessoId, fase.Id)).Should().BeNull("a fase pertence a outro processo");
        (await leitor.DaEtapaAsync(outroProcessoId, etapa.Id)).Should().BeNull("a etapa pertence a outro processo");
        (await leitor.DaFaseAsync(Guid.CreateVersion7(), fase.Id)).Should().BeNull("o processo não existe");
    }

    [Fact(DisplayName = "A fase só começa a contar como iniciada quando a janela publicada começou")]
    public async Task Iniciadas_ContaPelaJanelaPublicada()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela da fase", Abertura);
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        FasesIniciadasDoProcesso leitor = new(context);

        FasesIniciadas antes = await leitor.ObterAsync(processoId, DateTimeOffset.UtcNow);
        FasesIniciadas depois = await leitor.ObterAsync(processoId, Abertura.AddDays(1));
        FasesIniciadas semProcesso = await leitor.ObterAsync(Guid.CreateVersion7(), Abertura.AddDays(1));

        antes.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
        depois.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        semProcesso.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "O ato do item que a fase declara faz a fase contar como iniciada antes da janela")]
    public async Task Iniciadas_ContaPeloAtoDoItemDaFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato da fase", Abertura);
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de uma etapa faz a fase que a contém contar como iniciada")]
    public async Task Iniciadas_ContaPeloAtoDaEtapaDaFase()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS ato da etapa", Abertura);
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de um item de outra fase com o mesmo tipo não inicia a fase")]
    public async Task Iniciadas_AtoDeOutraFaseNaoIniciaAFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato de outra fase", Abertura);
        (_, FaseCronograma faseDeOutroProcesso, _) = await PublicadoAsync("PS outra fase", Abertura);
        IdentidadeDoItemDoEdital deOutraFase = IdentidadeDoItemDoEdital.DaFase(faseDeOutroProcesso, fase.Produtos.Single());
        await SemearAtoAsync(processoId, deOutraFase);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A importação de notas do ENEM numa etapa da fase faz a fase contar como iniciada")]
    public async Task Iniciadas_ContaPelaImportacaoDeNotasNaEtapa()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS avaliacao da etapa", Abertura);
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await PublicadoAsync("PS avaliacao de outra etapa", Abertura);
        await SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);
        FasesIniciadas deOutroProcesso = await IniciadasAsync(outroProcessoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    private async Task SemearImportacaoDeNotasAsync(Guid processoId, Guid etapaId, DateTimeOffset importadoEm)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        Guid versaoId = await semeadura.VersoesConfiguracao.Where(v => v.ProcessoSeletivoId == processoId).Select(v => v.Id).FirstAsync();
        semeadura.LotesDeNotasDoEnem.Add(LoteDeNotasDoEnem.Registrar(
            processoId, etapaId, "2026", "INEP_ENEM_RETORNO", "v1", new string('b', 64), new string('c', 64), "sub-admin", importadoEm, 0,
            versaoId, new ResultadoDoCruzamento([], [], [], []), []));
        await semeadura.SaveChangesAsync();
    }

    private async Task SemearInscricaoRecebidaAsync(Guid processoId, DateTimeOffset criadaEm)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        semeadura.Inscricoes.Add(InscricaoSubmetida(processoId, criadaEm));
        await semeadura.SaveChangesAsync();
    }

    private static InscricaoRecord InscricaoSubmetida(Guid processoId, DateTimeOffset criadaEm)
    {
        Guid id = Guid.CreateVersion7();
        return new InscricaoRecord
        {
            Id = id,
            ProcessoSeletivoId = processoId,
            CpfIndice = id.ToByteArray(),
            Sub = $"sub-{id:N}",
            EmissorToken = "https://issuer",
            Situacao = SituacaoInscricao.Submetida,
            VersaoFormularioId = Guid.CreateVersion7(),
            NumeroInscricao = $"UNI-{id.ToString("N")[^8..]}",
            Revisao = 1,
            CreatedAt = criadaEm,
        };
    }

    [Fact(DisplayName = "A versão de vigência futura não vale: a unidade da fase é a da versão que já vige")]
    public async Task DaFase_VersaoComVigenciaFutura_NaoVale()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS vigencia futura");
        await SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), f => f["unidadeResponsavel"]!["origemId"] = UnidadesDeTeste.Cga.OrigemId);
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        UnidadeDaFase? daFase = await new UnidadeDaFaseNaVersaoVigente(context, TimeProvider.System).DaFaseAsync(processoId, fase.Id);
        UnidadeDaFase? daEtapa = await new UnidadeDaFaseNaVersaoVigente(context, TimeProvider.System).DaEtapaAsync(processoId, etapa.Id);
        UnidadeDaFase? depoisDaVigencia = await new UnidadeDaFaseNaVersaoVigente(context, new RelogioFixo(DateTimeOffset.UtcNow.AddDays(31))).DaFaseAsync(processoId, fase.Id);

        daFase!.UnidadeId.Should().Be(UnidadesDeTeste.Ceps.OrigemId);
        daEtapa!.UnidadeId.Should().Be(UnidadesDeTeste.Ceps.OrigemId);
        depoisDaVigencia!.UnidadeId.Should().Be(UnidadesDeTeste.Cga.OrigemId, "a partir da vigência a sucessora vale");
    }

    [Fact(DisplayName = "A janela aberta numa versão e adiada na seguinte continua contando como fase iniciada")]
    public async Task Iniciadas_JanelaAdiadaNaRetificacao_ContinuaIniciada()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela adiada");
        await SucederAsync(processoId, DateTimeOffset.UtcNow, AdiaAJanela);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue("a janela da primeira versão já abriu");
    }

    [Fact(DisplayName = "A fase iniciada retirada numa versão e reinserida na seguinte continua contando como iniciada")]
    public async Task Iniciadas_FaseRetiradaEReinserida_ContinuaIniciada()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS fase retirada");
        await SucederAsync(processoId, DateTimeOffset.UtcNow, f => f["faseCanonicaOrigemId"] = Guid.CreateVersion7());
        await SucederAsync(processoId, DateTimeOffset.UtcNow, f => f["unidadeResponsavel"]!["origemId"] = UnidadesDeTeste.Cga.OrigemId);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    private static readonly DateTimeOffset Abertura = new(2040, 3, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "A janela adiada antes de abrir não faz a fase contar como iniciada")]
    public async Task Iniciadas_JanelaAdiadaAntesDeAbrir_NaoConta()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS adiada antes de abrir", Abertura);
        await SucederAsync(processoId, Abertura.AddDays(-9), AdiaAJanela);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, Abertura.AddDays(5));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse("a versão que previa a janela foi substituída antes de ela abrir");
    }

    [Fact(DisplayName = "A janela adiada depois de abrir continua contando como fase iniciada")]
    public async Task Iniciadas_JanelaAdiadaDepoisDeAbrir_Conta()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS adiada depois de abrir", Abertura);
        await SucederAsync(processoId, Abertura.AddDays(2), AdiaAJanela);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, Abertura.AddDays(5));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue("a janela abriu enquanto a versão que a previa vigia");
    }

    [Fact(DisplayName = "A versão de vigência futura não entra como evidência: a janela dela, já no passado, não inicia a fase")]
    public async Task Iniciadas_JanelaDeVersaoDeVigenciaFutura_NaoConta()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela de versao futura", Abertura);
        await SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), JanelaNoPassado);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse("a sucessora ainda não vige");
    }

    [Fact(DisplayName = "A inscrição recebida inicia só a fase que a recebeu, e não as outras que também coletam")]
    public async Task Iniciadas_InscricaoIniciaSoAFaseQueARecebeu()
    {
        (Guid processoId, FaseCronograma primeira, _) = await PublicadoAsync("PS inscricao em uma fase", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AcrescentaFaseQueColeta(envelope, segundaOrigem));
        await SemearInscricaoRecebidaAsync(processoId, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a inscrição foi recebida na primeira fase que coleta");
    }

    [Fact(DisplayName = "A importação de notas conta para a fase a que a etapa pertencia na versão vigente quando aconteceu")]
    public async Task Iniciadas_ImportacaoContaParaAFaseDaEtapaNaVersaoDaEpoca()
    {
        (Guid processoId, FaseCronograma primeira, EtapaProcesso etapa) = await PublicadoAsync("PS etapa movida", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), envelope => MoveAEtapaParaNovaFase(envelope, segundaOrigem));
        await SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow.AddDays(10));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow.AddDays(60));

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue("a etapa era da primeira fase quando as notas foram importadas");
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a etapa só passou à segunda fase depois da importação");
    }

    [Fact(DisplayName = "A importação contra a versão divulgada conta para a fase da etapa nela, mesmo com a retificação já fechada e não divulgada")]
    public async Task Iniciadas_ImportacaoUsaAVersaoFixadaNoLote()
    {
        (Guid processoId, FaseCronograma primeira, EtapaProcesso etapa) = await PublicadoAsync("PS etapa movida nao divulgada", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => MoveAEtapaParaNovaFase(envelope, segundaOrigem));
        await SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow.AddDays(5));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, DateTimeOffset.UtcNow.AddDays(10));

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue("o lote fixou a versão em que a etapa era da primeira fase");
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a versão da segunda fase ainda não foi divulgada");
    }

    private static void JanelaNoPassado(JsonObject fase)
    {
        fase["inicio"] = "2020-01-01T00:00:00+00:00";
        fase["fim"] = "2020-01-31T00:00:00+00:00";
    }

    // A fase nova nasce com a janela no futuro, para só a inscrição ou a importação poderem iniciá-la.
    private static JsonObject FaseNovaComAJanelaNoFuturo(JsonObject envelope, Guid origemId)
    {
        JsonArray fases = (JsonArray)envelope["cronogramaFases"]!["fases"]!;
        JsonObject nova = JsonNode.Parse(fases[0]!.ToJsonString())!.AsObject();
        nova["id"] = Guid.CreateVersion7();
        nova["faseCanonicaOrigemId"] = origemId;
        nova["codigo"] = "SEGUNDA_FASE";
        AdiaAJanela(nova);
        fases.Add(nova);
        return nova;
    }

    private static void AcrescentaFaseQueColeta(JsonObject envelope, Guid origemId) =>
        FaseNovaComAJanelaNoFuturo(envelope, origemId)["coletaInscricao"] = true;

    private static void MoveAEtapaParaNovaFase(JsonObject envelope, Guid origemId)
    {
        FaseNovaComAJanelaNoFuturo(envelope, origemId)["coletaInscricao"] = false;
        envelope["etapas"]!.AsArray()[0]!["faseCodigo"] = "SEGUNDA_FASE";
    }

    private static void AdiaAJanela(JsonObject fase)
    {
        fase["inicio"] = "2045-01-01T00:00:00+00:00";
        fase["fim"] = "2045-01-31T00:00:00+00:00";
    }

    private Task SucederAsync(Guid processoId, DateTimeOffset vigenteAPartirDe, Action<JsonObject> alteraAFase) =>
        SucederNoEnvelopeAsync(processoId, vigenteAPartirDe, envelope => alteraAFase((JsonObject)envelope["cronogramaFases"]!["fases"]![0]!));

    private async Task SucederNoEnvelopeAsync(Guid processoId, DateTimeOffset vigenteAPartirDe, Action<JsonObject> alteraOEnvelope)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        VersaoConfiguracao anterior = await context.VersoesConfiguracao
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        JsonObject envelope = JsonNode.Parse(anterior.ConfiguracaoCongelada)!.AsObject();
        alteraOEnvelope(envelope);
        context.VersoesConfiguracao.Add(VersaoConfiguracao.Suceder(
            anterior, Encoding.UTF8.GetBytes(envelope.ToJsonString()), anterior.SchemaVersion, anterior.AlgoritmoHash,
            Guid.CreateVersion7(), anterior.AtoCriadorHash, anterior.AtoCriadorId, "plataforma-admin", vigenteAPartirDe));
        await context.SaveChangesAsync();
    }

    private async Task<FasesIniciadas> IniciadasAsync(Guid processoId, DateTimeOffset agora)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        return await new FasesIniciadasDoProcesso(context).ObterAsync(processoId, agora);
    }

    private async Task SemearAtoAsync(Guid processoId, IdentidadeDoItemDoEdital item)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "resultado.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        semeadura.DocumentosAPublicar.Add(documento);
        semeadura.AtosDoProcesso.Add(AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, item.AtoCodigo, "Resultado", Numero: null,
                new DateOnly(2025, 12, 1), "Diretoria", "ceps", IdentificadoresDeTeste.NovoValor(), documento.Id, [], item),
            TimeProvider.System).Value!);
        await semeadura.SaveChangesAsync();
    }

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

    private async Task<(Guid ProcessoId, FaseCronograma Fase, EtapaProcesso Etapa)> PublicadoAsync(string nome, DateTimeOffset? inicioDaFase = null)
    {
        ProcessoSeletivoPublicacaoSeeder.Resultado resultado = await ProcessoSeletivoPublicacaoSeeder.PublicarAsync(_fixture, nome, inicioDaFase);
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await context.ProcessosSeletivos
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.Etapas)
            .SingleAsync(p => p.Id == resultado.ProcessoId);
        return (processo.Id, processo.CronogramaFases.Single(), processo.Etapas.Single());
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
