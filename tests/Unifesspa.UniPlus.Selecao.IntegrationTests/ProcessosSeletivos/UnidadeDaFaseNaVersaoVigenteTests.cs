namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static CenarioDeFasesDoProcessoPublicado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaUnidadeDaFaseNaVersaoVigente;

/// <summary>
/// A unidade da fase e a das fases iniciadas, lidas do envelope congelado da versão vigente num Postgres real.
/// </summary>
public sealed class UnidadeDaFaseNaVersaoVigenteTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;
    private readonly ApoioDaUnidadeDaFaseNaVersaoVigente _apoio;

    public UnidadeDaFaseNaVersaoVigenteTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);
        _apoio = new ApoioDaUnidadeDaFaseNaVersaoVigente(fixture);
    }

    [Fact(DisplayName = "A fase publicada resolve a unidade congelada na versão vigente")]
    public async Task DaFase_ResolveAUnidadeDaVersaoVigente()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS unidade da fase");
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        UnidadeDaFaseNaVersaoVigente leitor = new(context, TimeProvider.System);

        UnidadeDaFase? daFase = await leitor.DaFaseAsync(processoId, fase.Id);
        UnidadeDaFase? daEtapa = await leitor.DaEtapaAsync(processoId, etapa.Id);

        daFase.Should().Be(new UnidadeDaFase(fase.Id, UnidadesDeTeste.Ceps.OrigemId));
        daEtapa.Should().Be(daFase, "a etapa segue a unidade da fase que a contém");
    }

    [Fact(DisplayName = "Fase ou etapa inexistente, de outro processo ou de processo sem versão não resolvem unidade")]
    public async Task NaoResolve_FaseOuEtapaQueNaoSeEncontra()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS fase inexistente");
        (Guid outroProcessoId, _, _) = await _cenario.PublicadoAsync("PS outro processo");
        await using UniPlusDbContext context = _fixture.CreateDbContext();
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
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS janela da fase", Abertura);
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        FasesIniciadasDoProcesso leitor = new(context, TimeProvider.System, []);

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
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS ato da fase", Abertura);
        await _cenario.SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de uma etapa faz a fase que a contém contar como iniciada")]
    public async Task Iniciadas_ContaPeloAtoDaEtapaDaFase()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS ato da etapa", Abertura);
        await _cenario.SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de um item de outra fase com o mesmo tipo não inicia a fase")]
    public async Task Iniciadas_AtoDeOutraFaseNaoIniciaAFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS ato de outra fase", Abertura);
        (_, FaseCronograma faseDeOutroProcesso, _) = await _cenario.PublicadoAsync("PS outra fase", Abertura);
        IdentidadeDoItemDoEdital deOutraFase = IdentidadeDoItemDoEdital.DaFase(faseDeOutroProcesso, fase.Produtos.Single());
        await _cenario.SemearAtoAsync(processoId, deOutraFase);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A importação de notas do ENEM numa etapa da fase faz a fase contar como iniciada")]
    public async Task Iniciadas_ContaPelaImportacaoDeNotasNaEtapa()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS avaliacao da etapa", Abertura);
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await _cenario.PublicadoAsync("PS avaliacao de outra etapa", Abertura);
        await _apoio.SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);
        FasesIniciadas deOutroProcesso = await _cenario.IniciadasAsync(outroProcessoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A versão de vigência futura não vale: a unidade da fase é a da versão que já vige")]
    public async Task DaFase_VersaoComVigenciaFutura_NaoVale()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS vigencia futura");
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), f => f["unidadeResponsavel"]!["origemId"] = UnidadesDeTeste.Cga.OrigemId);
        await using UniPlusDbContext context = _fixture.CreateDbContext();

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
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS janela adiada");
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, AdiaAJanela);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue("a janela da primeira versão já abriu");
    }

    [Fact(DisplayName = "A fase iniciada retirada numa versão e reinserida na seguinte continua contando como iniciada")]
    public async Task Iniciadas_FaseRetiradaEReinserida_ContinuaIniciada()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS fase retirada");
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, f => f["faseCanonicaOrigemId"] = Guid.CreateVersion7());
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow, f => f["unidadeResponsavel"]!["origemId"] = UnidadesDeTeste.Cga.OrigemId);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "A janela adiada antes de abrir não faz a fase contar como iniciada")]
    public async Task Iniciadas_JanelaAdiadaAntesDeAbrir_NaoConta()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS adiada antes de abrir", Abertura);
        await _apoio.SucederAsync(processoId, Abertura.AddDays(-9), AdiaAJanela);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, Abertura.AddDays(5));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse("a versão que previa a janela foi substituída antes de ela abrir");
    }

    [Fact(DisplayName = "A janela adiada depois de abrir continua contando como fase iniciada")]
    public async Task Iniciadas_JanelaAdiadaDepoisDeAbrir_Conta()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS adiada depois de abrir", Abertura);
        await _apoio.SucederAsync(processoId, Abertura.AddDays(2), AdiaAJanela);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, Abertura.AddDays(5));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue("a janela abriu enquanto a versão que a previa vigia");
    }

    [Fact(DisplayName = "A versão de vigência futura não entra como evidência: a janela dela, já no passado, não inicia a fase")]
    public async Task Iniciadas_JanelaDeVersaoDeVigenciaFutura_NaoConta()
    {
        (Guid processoId, FaseCronograma fase, _) = await _cenario.PublicadoAsync("PS janela de versao futura", Abertura);
        await _apoio.SucederAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), JanelaNoPassado);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse("a sucessora ainda não vige");
    }

    [Fact(DisplayName = "A inscrição recebida inicia só a fase que a recebeu, e não as outras que também coletam")]
    public async Task Iniciadas_InscricaoIniciaSoAFaseQueARecebeu()
    {
        (Guid processoId, FaseCronograma primeira, _) = await _cenario.PublicadoAsync("PS inscricao em uma fase", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await _apoio.SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => AcrescentaFaseQueColeta(envelope, segundaOrigem));
        await _apoio.SemearInscricaoRecebidaAsync(processoId, DateTimeOffset.UtcNow);

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow);

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue();
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a inscrição foi recebida na primeira fase que coleta");
    }

    [Fact(DisplayName = "A importação de notas conta para a fase a que a etapa pertencia na versão vigente quando aconteceu")]
    public async Task Iniciadas_ImportacaoContaParaAFaseDaEtapaNaVersaoDaEpoca()
    {
        (Guid processoId, FaseCronograma primeira, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS etapa movida", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await _apoio.SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow.AddDays(30), envelope => MoveAEtapaParaNovaFase(envelope, segundaOrigem));
        await _apoio.SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow.AddDays(10));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow.AddDays(60));

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue("a etapa era da primeira fase quando as notas foram importadas");
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a etapa só passou à segunda fase depois da importação");
    }

    [Fact(DisplayName = "A importação contra a versão divulgada conta para a fase da etapa nela, mesmo com a retificação já fechada e não divulgada")]
    public async Task Iniciadas_ImportacaoUsaAVersaoFixadaNoLote()
    {
        (Guid processoId, FaseCronograma primeira, EtapaProcesso etapa) = await _cenario.PublicadoAsync("PS etapa movida nao divulgada", Abertura);
        Guid segundaOrigem = Guid.CreateVersion7();
        await _apoio.SucederNoEnvelopeAsync(processoId, DateTimeOffset.UtcNow, envelope => MoveAEtapaParaNovaFase(envelope, segundaOrigem));
        await _apoio.SemearImportacaoDeNotasAsync(processoId, etapa.Id, DateTimeOffset.UtcNow.AddDays(5));

        FasesIniciadas iniciadas = await _cenario.IniciadasAsync(processoId, DateTimeOffset.UtcNow.AddDays(10));

        iniciadas.Contem(primeira.FaseCanonicaOrigemId).Should().BeTrue("o lote fixou a versão em que a etapa era da primeira fase");
        iniciadas.Contem(segundaOrigem).Should().BeFalse("a versão da segunda fase ainda não foi divulgada");
    }
}
