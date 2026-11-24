namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A recusa da publicação diz, em cada erro de <c>errors[]</c> que passa por um item de lista, qual é o item: o código da fase, o
/// identificador da etapa e o do recurso. O cliente acha o item por eles, e não pela posição, que muda quando outra pessoa grava o
/// processo. A recusa usada é a da regra de prazo do recurso que cita a versão do catálogo com conteúdo diferente do atual.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed partial class IdentificadoresDasRecusasDaPublicacaoTests
{
    private const string CodigoDaRecusa = "uniplus.selecao.processo_seletivo.regra_citada_nao_corresponde_ao_catalogo";
    private const string CodigoDaFaseSemeada = "RESULTADO_FINAL";

    private static readonly string HashDiferenteDoCatalogo = new('0', 64);

    private readonly CascadingFixture _fixture;

    public IdentificadoresDasRecusasDaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A recusa da regra de recurso da fase traz o código da fase em identificadores")]
    public async Task RecusaDaRegraDeRecursoDaFase_TrazOCodigoDaFase()
    {
        JsonElement problema = await PublicarComRegraDeRecursoDaFaseDivergenteAsync();

        JsonElement erro = ErroNoCampo(problema, "/cronograma/fases/0/regraRecurso/regraCodigo");

        Identificadores(erro).Should().Equal(("fases", CodigoDaFaseSemeada));
        ExigirFormaDoContrato(problema);
    }

    [Fact(DisplayName = "A recusa da regra de recurso da etapa traz o identificador da etapa e o do recurso, na posição que o campo emite")]
    public async Task RecusaDaRegraDeRecursoDaEtapa_TrazAEtapaEORecursoNaOrdemDoIndiceEmitido()
    {
        (JsonElement problema, Guid etapaId, int posicao, Guid recursoId) = await PublicarComRegraDeRecursoDaEtapaDivergenteAsync();

        JsonElement erro = ErroNoCampo(problema, $"/etapas/0/recursos/{posicao}/regraCodigo");

        posicao.Should().Be(1, "o recurso recusado é o segundo na ordem canônica, e não o segundo declarado");
        Identificadores(erro).Should().Equal(("etapas", etapaId.ToString("D")), ("recursos", recursoId.ToString("D")));
        ExigirFormaDoContrato(problema);
    }

    [Fact(DisplayName = "Depois que outra pessoa reordena as fases e as etapas e regrava os recursos, os identificadores da recusa ainda localizam os itens na leitura, e o identificador antigo do recurso não localiza nenhum")]
    public async Task RecusaDaPublicacao_DepoisDeOutraGravacao_IdentificaOsItensPelaLeitura()
    {
        ProcessoComListasReordenaveis processo = await ProcessoComListasReordenaveis.SemearAsync(
            _fixture, nameof(RecusaDaPublicacao_DepoisDeOutraGravacao_IdentificaOsItensPelaLeitura), await RegraDePrazoDoCatalogoAsync());
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonElement recusaAntiga = await PublicarRecusadaAsync(processo.ProcessoId, processo.DocumentoId);
        ProcessoSeletivoDto lidoAntes = await LerProcessoPelaApiAsync(client, processo.ProcessoId);
        PosicoesLidas antes = ConferirOsIndicesDaRecusaContraALeitura(recusaAntiga, lidoAntes, processo.EtapaId, processo.RecursoDaCienciaId);

        await processo.OutraPessoaGravaAsync();
        ProcessoSeletivoDto lidoDepois = await LerProcessoPelaApiAsync(client, processo.ProcessoId);
        ExigirQueOsIdentificadoresAindaLocalizamOsItens(recusaAntiga, antes, lidoDepois);
        ExigirOsIndicesDaNovaRecusa(await PublicarRecusadaAsync(processo.ProcessoId, processo.DocumentoId), lidoDepois, processo.EtapaId);
    }

    private async Task<JsonElement> PublicarComRegraDeRecursoDaFaseDivergenteAsync()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelDaPublicacaoPrevista.Preliminar);
        ProdutoDaFase definitivo = ProdutoDaFase.Criar(HomologacaoDasInscricoesDeTeste.ResultadoDaFase, PapelDaPublicacaoPrevista.Definitivo);
        RegraRecursoFase regra = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", HashDiferenteDoCatalogo),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            preliminar.Id).Value!;

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(RecusaDaRegraDeRecursoDaFase_TrazOCodigoDaFase),
            new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, ProdutosDaFase = [preliminar, definitivo], RegraDeRecursoDaFase = regra });

        return await PublicarRecusadaAsync(processo.Id, documento.Id);
    }

    private async Task<(JsonElement Problema, Guid EtapaId, int Posicao, Guid RecursoId)> PublicarComRegraDeRecursoDaEtapaDivergenteAsync()
    {
        ReferenciaRegra doCatalogo = await RegraDePrazoDoCatalogoAsync();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearAsync(
            nameof(RecusaDaRegraDeRecursoDaEtapa_TrazAEtapaEORecursoNaOrdemDoIndiceEmitido),
            new OpcoesDoProcessoSeletivoPublicavel { Nome = string.Empty, Complementar = p => ComDoisRecursosNaEtapa(p, doCatalogo) });
        EtapaProcesso etapa = processo.Etapas.Single();
        RecursoDaEtapa[] naOrdemCanonica = [.. etapa.Recursos];
        int posicao = Array.FindIndex(naOrdemCanonica, static r => r.Ancora == EventoInicialDoPrazo.CienciaIndividual);

        return (await PublicarRecusadaAsync(processo.Id, documento.Id), etapa.Id, posicao, naOrdemCanonica[posicao].Id);
    }

    // Declarados na ordem inversa da canônica (o prazo da ciência vem depois do prazo do ato): só o primeiro declarado cita a regra com
    // conteúdo diferente do catálogo, e a posição dele na lista da etapa difere da posição em que foi declarado.
    private static void ComDoisRecursosNaEtapa(ProcessoSeletivo processo, ReferenciaRegra doCatalogo)
    {
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelDaPublicacaoPrevista.Preliminar)]).IsSuccess.Should().BeTrue();

        Guid produtoPreliminar = etapa.Produtos.Single().Id;
        ReferenciaRegra divergente = ReferenciaRegra.De(doCatalogo.Codigo, doCatalogo.Versao, HashDiferenteDoCatalogo);
        etapa.DefinirRecursos(
        [
            RecursoDaEtapaDe(EventoInicialDoPrazo.CienciaIndividual, divergente, Guid.Empty),
            RecursoDaEtapaDe(EventoInicialDoPrazo.PublicacaoDoAto, doCatalogo, produtoPreliminar),
        ]).IsSuccess.Should().BeTrue();
    }

    private static RecursoDaEtapa RecursoDaEtapaDe(EventoInicialDoPrazo ancora, ReferenciaRegra regra, Guid produtoAncoraId) =>
        RecursoDaEtapa.Criar(ancora, regra, ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas), produtoAncoraId).Value!;

    private async Task<ReferenciaRegra> RegraDePrazoDoCatalogoAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return (await db.RolDeRegras.AsNoTracking().SingleAsync(regra =>
            regra.Codigo == RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo() && regra.Versao == "v1")).Referencia();
    }

    private async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearAsync(string cenario, OpcoesDoProcessoSeletivoPublicavel opcoes)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

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

    private static JsonElement ErroNoCampo(JsonElement problema, string campo) =>
        problema.GetProperty("errors").EnumerateArray().Single(erro =>
            erro.GetProperty("code").GetString() == CodigoDaRecusa && erro.GetProperty("field").GetString() == campo);

    private static (string Lista, string Identificador)[] Identificadores(JsonElement erro) =>
    [
        .. erro.GetProperty("identificadores").EnumerateArray()
            .Select(static item => (item.GetProperty("lista").GetString()!, item.GetProperty("identificador").GetString()!)),
    ];

    // O contrato de cada item de identificadores: só a lista e o identificador, a lista é uma das três que a configuração tem, e a
    // mensagem do erro não repete o identificador interno (que é código de fase ou identificador opaco, nunca dado de pessoa).
    private static void ExigirFormaDoContrato(JsonElement problema)
    {
        string[] listasConhecidas = ["fases", "etapas", "recursos"];
        JsonElement[] comIdentificadores =
            [.. problema.GetProperty("errors").EnumerateArray().Where(static erro => erro.TryGetProperty("identificadores", out _))];

        comIdentificadores.Should().NotBeEmpty();
        foreach (JsonElement erro in comIdentificadores)
        {
            erro.GetProperty("message").GetString().Should().NotMatchRegex(GuidNoTexto());
            foreach (JsonElement item in erro.GetProperty("identificadores").EnumerateArray())
            {
                item.EnumerateObject().Select(static p => p.Name).Should().BeEquivalentTo("lista", "identificador");
                listasConhecidas.Should().Contain(item.GetProperty("lista").GetString());
            }
        }
    }

    // O índice de cada erro é a posição do item na lista que a leitura do processo devolve: a segunda fase, a segunda etapa e o segundo
    // recurso da etapa (a ciência individual vem depois do ato publicado na ordem canônica).
    private static PosicoesLidas ConferirOsIndicesDaRecusaContraALeitura(JsonElement problema, ProcessoSeletivoDto lido, Guid etapaId, Guid recursoId)
    {
        int fase = lido.CronogramaFases.ToList().FindIndex(f => f.Codigo == ProcessoComListasReordenaveis.CodigoDaFaseComRecurso);
        int etapa = lido.Etapas.ToList().FindIndex(e => e.Id == etapaId);
        int recurso = lido.Etapas[etapa].Publicacoes.Recursos.ToList().FindIndex(r => r.Id == recursoId);

        ErroNoCampo(problema, $"/cronograma/fases/{fase}/regraRecurso/regraCodigo");
        ErroNoCampo(problema, $"/etapas/{etapa}/recursos/{recurso}/regraCodigo");

        return new PosicoesLidas(fase, etapa, recurso);
    }

    private static void ExigirOsIndicesDaNovaRecusa(JsonElement recusa, ProcessoSeletivoDto lido, Guid etapaId)
    {
        Guid recursoId = lido.Etapas.Single(e => e.Id == etapaId).Publicacoes.Recursos.Single(r => r.Ancora.Codigo == EventoInicialDoPrazo.CienciaIndividual.Codigo()).Id;

        PosicoesLidas depois = ConferirOsIndicesDaRecusaContraALeitura(recusa, lido, etapaId, recursoId);

        depois.Should().Be(new PosicoesLidas(Fase: 0, Etapa: 0, Recurso: 1), "as duas listas foram reordenadas e a recusa acompanha a nova posição de cada item");
        Identificadores(ErroNoCampo(recusa, "/etapas/0/recursos/1/regraCodigo")).Should().Equal(("etapas", etapaId.ToString("D")), ("recursos", recursoId.ToString("D")));
    }

    // Os identificadores da recusa anterior, guardados por quem a recebeu: nenhum depende da posição que o item tinha na lista.
    private static void ExigirQueOsIdentificadoresAindaLocalizamOsItens(JsonElement recusaAntiga, PosicoesLidas antes, ProcessoSeletivoDto depois)
    {
        (string Lista, string Identificador)[] daFase = Identificadores(ErroNoCampo(recusaAntiga, $"/cronograma/fases/{antes.Fase}/regraRecurso/regraCodigo"));
        (string Lista, string Identificador)[] daEtapa = Identificadores(ErroNoCampo(recusaAntiga, $"/etapas/{antes.Etapa}/recursos/{antes.Recurso}/regraCodigo"));

        antes.Should().Be(new PosicoesLidas(Fase: 1, Etapa: 1, Recurso: 1), "o item recusado é o segundo de cada lista");
        ExigirQueOCodigoLocalizaAFase(daFase.Single().Identificador, antes.Fase, depois);
        ExigirQueOIdentificadorLocalizaAEtapa(Guid.Parse(daEtapa[0].Identificador), antes.Etapa, depois);
        ExigirQueOIdentificadorAntigoNaoLocalizaRecurso(Guid.Parse(daEtapa[1].Identificador), depois);
    }

    private static void ExigirQueOCodigoLocalizaAFase(string codigo, int posicaoAntes, ProcessoSeletivoDto depois)
    {
        int posicao = depois.CronogramaFases.ToList().FindIndex(f => f.Codigo == codigo);

        posicao.Should().BeGreaterThanOrEqualTo(0).And.NotBe(posicaoAntes, "a outra gravação mudou a posição da fase");
        depois.CronogramaFases[posicao].Publicacoes.RegraRecurso.Should().NotBeNull("a fase localizada pelo código é a que declara o recurso");
        depois.CronogramaFases[posicaoAntes].Codigo.Should().NotBe(codigo, "a posição antiga passou a ser de outra fase");
    }

    private static void ExigirQueOIdentificadorLocalizaAEtapa(Guid etapaId, int posicaoAntes, ProcessoSeletivoDto depois)
    {
        int posicao = depois.Etapas.ToList().FindIndex(e => e.Id == etapaId);

        posicao.Should().BeGreaterThanOrEqualTo(0).And.NotBe(posicaoAntes, "a outra gravação mudou a posição da etapa");
        depois.Etapas[posicao].Identificacao.Nome.Should().Be(ProcessoComListasReordenaveis.NomeDaEtapaComRecursos);
        depois.Etapas[posicaoAntes].Id.Should().NotBe(etapaId, "a posição antiga passou a ser de outra etapa");
    }

    private static void ExigirQueOIdentificadorAntigoNaoLocalizaRecurso(Guid recursoAntigoId, ProcessoSeletivoDto depois)
    {
        List<Guid> recursos =
        [
            .. depois.Etapas.SelectMany(e => e.Publicacoes.Recursos).Select(r => r.Id),
            .. depois.CronogramaFases.Where(f => f.Publicacoes.RegraRecurso is not null).Select(f => f.Publicacoes.RegraRecurso!.Id),
        ];

        recursos.Should().HaveCount(3).And.NotContain(recursoAntigoId, "o recurso gravado de novo ganhou identificador novo, e o antigo não aponta nenhum outro");
    }

    private sealed record PosicoesLidas(int Fase, int Etapa, int Recurso);

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GuidNoTexto();
}
