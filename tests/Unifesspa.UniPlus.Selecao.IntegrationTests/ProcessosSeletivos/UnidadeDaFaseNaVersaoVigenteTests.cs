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
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS janela da fase");
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        FasesIniciadasDoProcesso leitor = new(context);

        FasesIniciadas antes = await leitor.ObterAsync(processoId, fase.Inicio!.Value.AddDays(-1));
        FasesIniciadas depois = await leitor.ObterAsync(processoId, fase.Inicio!.Value.AddDays(1));
        FasesIniciadas semProcesso = await leitor.ObterAsync(Guid.CreateVersion7(), fase.Inicio!.Value.AddDays(1));

        antes.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
        depois.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        semProcesso.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "O ato do item que a fase declara faz a fase contar como iniciada antes da janela")]
    public async Task Iniciadas_ContaPeloAtoDoItemDaFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato da fase");
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaFase(fase, fase.Produtos.Single()));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de uma etapa faz a fase que a contém contar como iniciada")]
    public async Task Iniciadas_ContaPeloAtoDaEtapaDaFase()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS ato da etapa");
        await SemearAtoAsync(processoId, IdentidadeDoItemDoEdital.DaEtapa(etapa, ProdutoDaEtapa.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)));

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
    }

    [Fact(DisplayName = "O ato de um item de outra fase com o mesmo tipo não inicia a fase")]
    public async Task Iniciadas_AtoDeOutraFaseNaoIniciaAFase()
    {
        (Guid processoId, FaseCronograma fase, _) = await PublicadoAsync("PS ato de outra fase");
        (_, FaseCronograma faseDeOutroProcesso, _) = await PublicadoAsync("PS outra fase");
        IdentidadeDoItemDoEdital deOutraFase = IdentidadeDoItemDoEdital.DaFase(faseDeOutroProcesso, fase.Produtos.Single());
        await SemearAtoAsync(processoId, deOutraFase);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    [Fact(DisplayName = "A avaliação já lançada numa etapa da fase faz a fase contar como iniciada")]
    public async Task Iniciadas_ContaPelaAvaliacaoLancadaNaEtapa()
    {
        (Guid processoId, FaseCronograma fase, EtapaProcesso etapa) = await PublicadoAsync("PS avaliacao da etapa");
        (Guid outroProcessoId, FaseCronograma outraFase, _) = await PublicadoAsync("PS avaliacao de outra etapa");
        await SemearAvaliacaoAsync(etapa.Id);

        FasesIniciadas iniciadas = await IniciadasAsync(processoId, fase.Inicio!.Value.AddDays(-30));
        FasesIniciadas deOutroProcesso = await IniciadasAsync(outroProcessoId, outraFase.Inicio!.Value.AddDays(-30));

        iniciadas.Contem(fase.FaseCanonicaOrigemId).Should().BeTrue();
        deOutroProcesso.Contem(outraFase.FaseCanonicaOrigemId).Should().BeFalse();
    }

    // As chaves estrangeiras do lote e da inscrição ficam fora da semeadura: o que se verifica é só a leitura.
    private async Task SemearAvaliacaoAsync(Guid etapaId)
    {
        await using SelecaoDbContext semeadura = _fixture.CreateDbContext();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transacao = await semeadura.Database.BeginTransactionAsync();
        await semeadura.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
        await semeadura.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO selecao.avaliacoes_da_etapa (id, lote_id, etapa_processo_id, inscricao_id, oferta_curso_id, grupo_area_enem_codigo, situacao, nota)
            VALUES ({Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {etapaId}, {Guid.CreateVersion7()}, {Guid.CreateVersion7()}, 'TECNOLOGICA', 1, 600.5)
            """);
        await transacao.CommitAsync();
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

    private static void AdiaAJanela(JsonObject fase)
    {
        fase["inicio"] = "2045-01-01T00:00:00+00:00";
        fase["fim"] = "2045-01-31T00:00:00+00:00";
    }

    private async Task SucederAsync(Guid processoId, DateTimeOffset vigenteAPartirDe, Action<JsonObject> alteraAFase)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        VersaoConfiguracao anterior = await context.VersoesConfiguracao
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        JsonObject envelope = JsonNode.Parse(anterior.ConfiguracaoCongelada)!.AsObject();
        alteraAFase((JsonObject)envelope["cronogramaFases"]!["fases"]![0]!);
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
