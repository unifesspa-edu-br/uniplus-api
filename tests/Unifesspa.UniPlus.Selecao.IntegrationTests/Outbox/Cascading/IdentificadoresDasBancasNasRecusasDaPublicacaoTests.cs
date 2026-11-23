namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A recusa da publicação diz, em cada erro de <c>errors[]</c> que aponta uma banca da fase ou da etapa, qual é a banca: depois do
/// código da fase ou do identificador da etapa vem o identificador da banca, o da banca que está na posição que o campo aponta na
/// ordem canônica. O cliente acha a banca por ele, e não pela posição, que muda quando outra é retirada. A recusa usada é a da
/// banca sem a regra de deliberação, e a que se refere à fase ou à etapa inteira não leva identificador de banca.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed partial class IdentificadoresDasBancasNasRecusasDaPublicacaoTests
{
    private const string CodigoDaBancaSemRegra = "uniplus.selecao.banca.regra_de_deliberacao_obrigatoria";
    private const string CodigoDoRecursoSemBanca = "uniplus.selecao.cronograma.recurso_sem_banca";
    private const string CodigoDaFaseComBancas = "CLASSIFICACAO";
    private const string CodigoDaFaseDeResultado = "RESULTADO_FINAL";
    private const string NomeDaEtapaComBancas = "Prova Discursiva";
    private const TipoDeBanca AnaliseDocumental = TipoDeBanca.AnaliseDocumental;
    private const TipoDeBanca Heteroidentificacao = TipoDeBanca.Heteroidentificacao;

    private readonly CascadingFixture _fixture;

    public IdentificadoresDasBancasNasRecusasDaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A recusa de cada banca da fase traz o código da fase e o identificador da banca na posição canônica, e não na ordem de chegada; duas bancas do mesmo tipo têm identificadores distintos")]
    public async Task RecusaDaBancaDaFase_TrazAFaseEABancaNaPosicaoCanonica()
    {
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(RecusaDaBancaDaFase_TrazAFaseEABancaNaPosicaoCanonica), new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, FasesAdicionais = [FaseComBancasSemRegra()] });
        BancaRequerida[] naOrdemCanonica = [.. processo.CronogramaFases.Single(fase => fase.Codigo == CodigoDaFaseComBancas).BancasRequeridas];

        JsonElement problema = await PublicarRecusadaAsync(processo.Id, documento.Id);

        naOrdemCanonica.Select(static banca => banca.Tipo.Codigo).Should().Equal(
            [AnaliseDocumental, AnaliseDocumental, Heteroidentificacao], "a banca de heteroidentificação foi declarada primeira e é a última na ordem canônica");
        ExigirAsBancasRecusadas(
            problema, "/cronograma/fases/1/bancasRequeridas/",
            [.. naOrdemCanonica.Select(static banca => new[] { $"fases={CodigoDaFaseComBancas}", $"bancasRequeridas={banca.Id:D}" })]);
        naOrdemCanonica.Select(static banca => banca.Id).Distinct().Should().HaveCount(3);
    }

    [Fact(DisplayName = "A recusa de cada banca da etapa traz o identificador da etapa e o da banca na posição canônica, e não na ordem de chegada")]
    public async Task RecusaDaBancaDaEtapa_TrazAEtapaEABancaNaPosicaoCanonica()
    {
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(RecusaDaBancaDaEtapa_TrazAEtapaEABancaNaPosicaoCanonica), new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, EtapasAdicionais = [EtapaComBancasSemRegra()] });
        EtapaProcesso etapa = processo.Etapas.Single(e => e.Nome == NomeDaEtapaComBancas);
        BancaDaEtapa[] naOrdemCanonica = [.. etapa.Bancas];

        JsonElement problema = await PublicarRecusadaAsync(processo.Id, documento.Id);

        naOrdemCanonica.Select(static banca => banca.Tipo.Codigo).Should().Equal([AnaliseDocumental, AnaliseDocumental, Heteroidentificacao]);
        ExigirAsBancasRecusadas(
            problema, "/etapas/1/bancas/", [.. naOrdemCanonica.Select(banca => new[] { $"etapas={etapa.Id:D}", $"bancas={banca.Id:D}" })]);
    }

    [Fact(DisplayName = "A banca retirada depois da recusa não aparece na nova recusa, e a banca que ficou mantém o identificador ao mudar de posição")]
    public async Task BancaRetirada_NaoApareceNaNovaRecusa_EAQueFicouMantemOIdentificador()
    {
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(BancaRetirada_NaoApareceNaNovaRecusa_EAQueFicouMantemOIdentificador), new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, EtapasAdicionais = [EtapaComBancasSemRegra()] });
        BancaDaEtapa[] antes = [.. processo.Etapas.Single(e => e.Nome == NomeDaEtapaComBancas).Bancas];
        ErroDeBanca[] recusaAntiga = ErrosDasBancas(await PublicarRecusadaAsync(processo.Id, documento.Id), "/etapas/1/bancas/");

        await OutraPessoaRetiraAPrimeiraBancaAsync(processo.Id, antes[0].Id);
        ErroDeBanca[] recusaNova = ErrosDasBancas(await PublicarRecusadaAsync(processo.Id, documento.Id), "/etapas/1/bancas/");

        recusaNova.Should().HaveCount(2, "sobraram duas bancas");
        recusaNova.SelectMany(static erro => erro.Identificadores).Should().NotContain($"bancas={antes[0].Id:D}");
        recusaNova.Select(static erro => erro.Identificadores[1]).Should().Equal($"bancas={antes[1].Id:D}", $"bancas={antes[2].Id:D}");
        recusaAntiga[1].Identificadores[1].Should().Be(recusaNova[0].Identificadores[1], "a banca que era a segunda passou a ser a primeira e mantém o identificador");
    }

    [Fact(DisplayName = "A recusa do recurso sem banca, que se refere à fase inteira, traz só o código da fase, sem identificador de banca")]
    public async Task RecusaDoRecursoSemBanca_TrazSoACodigoDaFase()
    {
        ReferenciaRegra doCatalogo = await RegraDePrazoDoCatalogoAsync();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(RecusaDoRecursoSemBanca_TrazSoACodigoDaFase), FaseDeResultadoComRecursoSemBanca(doCatalogo));

        JsonElement problema = await PublicarRecusadaAsync(processo.Id, documento.Id);

        JsonElement erro = ErroDoCodigo(problema, CodigoDoRecursoSemBanca, "/cronograma/fases/0/bancasRequeridas");
        Identificadores(erro).Should().Equal([$"fases={CodigoDaFaseDeResultado}"], "a lista das bancas sem posição não aponta banca nenhuma");
    }

    [Fact(DisplayName = "A recusa do campo fases, sem índice, não traz identificadores")]
    public async Task RecusaDoCampoFasesSemIndice_NaoTrazIdentificadores()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPendenciasSeeder.SemearComCronogramaNaoConformeAsync(
            db, $"{nameof(RecusaDoCampoFasesSemIndice_NaoTrazIdentificadores)} {Guid.CreateVersion7()}");

        JsonElement problema = await PublicarRecusadaAsync(processo.Id, documento.Id);

        JsonElement erro = ErroDoCodigo(problema, "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta", "/cronograma/fases");
        erro.TryGetProperty("identificadores", out _).Should().BeFalse("o membro sai omitido, nunca nulo nem vazio");
    }

    // Cada banca recusada está na posição canônica que o campo aponta, e o que o erro traz é o dono (fase ou etapa) e a banca dessa posição.
    private static void ExigirAsBancasRecusadas(JsonElement problema, string prefixoDoCampo, string[][] identificadoresEsperados)
    {
        ErroDeBanca[] erros = ErrosDasBancas(problema, prefixoDoCampo);

        erros.Select(static erro => erro.Campo).Should().Equal([.. identificadoresEsperados.Select((_, posicao) => $"{prefixoDoCampo}{posicao}")]);
        erros.Select(static erro => erro.Identificadores).Should().BeEquivalentTo(identificadoresEsperados, opcoes => opcoes.WithStrictOrdering());
        ExigirFormaDoContrato(problema);
    }

    // O contrato de cada item de identificadores: só a lista e o identificador, a lista é uma das que a configuração tem, e a mensagem
    // do erro não repete o identificador interno.
    private static void ExigirFormaDoContrato(JsonElement problema)
    {
        string[] listasConhecidas = ["fases", "etapas", "recursos", "bancasRequeridas", "bancas"];

        foreach (JsonElement erro in problema.GetProperty("errors").EnumerateArray().Where(static erro => erro.TryGetProperty("identificadores", out _)))
        {
            erro.GetProperty("message").GetString().Should().NotMatchRegex(GuidNoTexto());
            foreach (JsonElement item in erro.GetProperty("identificadores").EnumerateArray())
            {
                item.EnumerateObject().Select(static p => p.Name).Should().BeEquivalentTo("lista", "identificador");
                listasConhecidas.Should().Contain(item.GetProperty("lista").GetString());
            }
        }
    }

    private static ErroDeBanca[] ErrosDasBancas(JsonElement problema, string prefixoDoCampo) =>
    [
        .. problema.GetProperty("errors").EnumerateArray()
            .Where(erro => erro.GetProperty("code").GetString() == CodigoDaBancaSemRegra
                && erro.GetProperty("field").GetString()!.StartsWith(prefixoDoCampo, StringComparison.Ordinal))
            .Select(static erro => new ErroDeBanca(erro.GetProperty("field").GetString()!, Identificadores(erro))),
    ];

    private static JsonElement ErroDoCodigo(JsonElement problema, string codigo, string campo) =>
        problema.GetProperty("errors").EnumerateArray().Single(erro =>
            erro.GetProperty("code").GetString() == codigo && erro.GetProperty("field").GetString() == campo);

    // Cada item como "lista=identificador", na ordem em que o erro os traz; o erro sem o membro não tem item.
    private static string[] Identificadores(JsonElement erro) =>
        erro.TryGetProperty("identificadores", out JsonElement itens)
            ? [.. itens.EnumerateArray().Select(static item => $"{item.GetProperty("lista").GetString()}={item.GetProperty("identificador").GetString()}")]
            : [];

    // As bancas chegam fora da ordem canônica (o código do tipo decide: a de análise documental vem antes da de heteroidentificação), e há
    // duas do mesmo tipo, distinguidas pelos candidatos atendidos. Nenhuma declara a regra de deliberação.
    private static FaseCronograma FaseComBancasSemRegra()
    {
        Result<FaseCronograma> fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), CodigoDaFaseComBancas, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(2, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: [BancaDaFase(Heteroidentificacao), BancaDaFase(AnaliseDocumental, "RENDA"), BancaDaFase(AnaliseDocumental, "PCD")]);

        fase.IsSuccess.Should().BeTrue(fase.Error?.Message);

        return fase.Value!;
    }

    private static BancaRequerida BancaDaFase(TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo), [.. categorias.Select(static codigo => CategoriaJulgada.Criar(Guid.CreateVersion7(), codigo))]);

    private static EtapaProcesso EtapaComBancasSemRegra()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(
                NomeDaEtapaComBancas, CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_DISCURSIVA", "Prova Discursiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                Ordem: 2, CodigoDaFaseDeResultado),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirBancas(
        [
            BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(Heteroidentificacao)),
            BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(AnaliseDocumental)),
            BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(AnaliseDocumental)),
        ]);

        return etapa;
    }

    // A fase única do processo admite recurso, e nem ela nem as etapas têm banca que o julgue.
    private static OpcoesDoProcessoSeletivoPublicavel FaseDeResultadoComRecursoSemBanca(ReferenciaRegra regraDoCatalogo)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelProdutoFase.Definitivo);
        RegraRecursoFase regra = RegraRecursoFase.Criar(regraDoCatalogo, ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas), preliminar.Id).Value!;

        return new() { Nome = string.Empty, ProdutosDaFase = [preliminar, definitivo], RegraDeRecursoDaFase = regra };
    }

    // O que outra pessoa faria ao editar a etapa: declara de novo as bancas que ficam, cada uma apontando a banca gravada que substitui,
    // e deixa de fora a retirada.
    private async Task OutraPessoaRetiraAPrimeiraBancaAsync(Guid processoId, Guid bancaRetiradaId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(processoId))!;
        EtapaProcesso etapa = processo.Etapas.Single(e => e.Nome == NomeDaEtapaComBancas);

        etapa.DefinirBancas([.. etapa.Bancas.Where(banca => banca.Id != bancaRetiradaId).Select(static banca =>
            BancaDaEtapa.Criar(banca.Tipo, banca.RegraDeDeliberacao, idDaBancaQueSubstitui: banca.Id))]);
        await db.SaveChangesAsync();
    }

    private async Task<ReferenciaRegra> RegraDePrazoDoCatalogoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return (await db.RolDeRegras.AsNoTracking().SingleAsync(regra =>
            regra.Codigo == RegraPrazoRecursoCodigo.AncoradoEmAto && regra.Versao == "v1")).Referencia();
    }

    private async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(string cenario, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes with { Nome = $"{cenario} {Guid.CreateVersion7()}" });
    }

    private async Task<JsonElement> PublicarRecusadaAsync(Guid processoId, Guid documentoId)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);
        string corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using JsonDocument problema = JsonDocument.Parse(corpo);

        return problema.RootElement.Clone();
    }

    private sealed record ErroDeBanca(string Campo, string[] Identificadores);

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GuidNoTexto();
}
