namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A cadeia completa do envelope fechado (issue #1045, story-pai #40): um processo RICO,
/// publicado pelo caminho HTTP com todas as dimensões que a cascata #1059/#1067/#1068/#563/#1069
/// entregou, o ato confirmado em Publicações — não só enfileirado —, uma sessão editorial que
/// altera e fecha, o segundo ato confirmado retificando o primeiro, e a versão vigente refletindo
/// o fechamento.
/// </summary>
/// <remarks>
/// <para>
/// Nenhum cenário isolado aqui é novo: cada elo já tem dono (§2 do plano da issue —
/// <c>RegistroDeAtoPorFilaDuravelTests</c>, <c>CicloDaRetificacaoEndpointTests</c>,
/// <c>EnvelopeCodecRoundTripTests</c>, <c>PoliticaDeOrdenacaoTests</c>). O que não existia em teste
/// algum era a <b>corrente inteira</b>, pelo caminho HTTP, com um processo que exercita fatos
/// coletados, cascata de remanejamento, divulgação e formulário — o corpus rico só era exercitado
/// por chamada direta ao domínio (<c>CorpusEnvelope</c>), nunca publicado por HTTP.
/// </para>
/// <para>
/// A golden fixture de <c>CorpusEnvelope</c> <b>não é oráculo</b> deste teste: a API de cronograma
/// resolve <c>FaseCanonicaId</c>/<c>TiposBancaIds</c> contra o catálogo vivo de Configuração, e o
/// envelope produzido por HTTP é semanticamente equivalente ao do corpus de domínio, não idêntico
/// byte a byte. Os catálogos que os handlers resolvem (fases canônicas, tipos de banca, ofertas de
/// curso, referência de reserva demográfica, unidade administradora) não têm <c>HasData</c> — são
/// semeados aqui, diretamente pelo <c>DbContext</c> de cada módulo, sob o mesmo Postgres
/// compartilhado que a <see cref="CascadingFixture"/> já provisiona para o host inteiro. Falta de
/// seed de catálogo é falha de arranjo, nunca evidência de endpoint ausente.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class EnvelopeFechadoE2ETests
{
    /// <summary>
    /// O código do ato que a fase de avaliação produz (FaseCronograma exige um quando
    /// <c>ProduzResultado</c> é <see langword="true"/>) — não congela configuração (é
    /// resultado, não edital/retificação) e não é único por objeto (um certame pode ter
    /// mais de um resultado ao longo do ciclo).
    /// </summary>
    private const string CodigoAtoResultadoPreliminar = "RESULTADO_PRELIMINAR";

    private readonly CascadingFixture _fixture;

    public EnvelopeFechadoE2ETests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName =
        "Ciclo completo pelo HTTP: criar → configurar todas as dimensões → GET /conformidade verde → " +
        "publicar (ato V1 confirmado em Publicações) → abrir/alterar/fechar retificação (ato V2 confirmado, " +
        "retificando V1) → GET /snapshot-vigente reflete V2, V1 permanece intacta")]
    public async Task CicloCompleto_DoHttpAoAtoConfirmadoEmPublicacoes()
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();
        string sufixo = Guid.CreateVersion7().ToString("N")[..8];

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeAtoResultadoPreliminarAsync(api);
        CatalogosSemeados catalogos = await SemearCatalogosRicosAsync(api, sufixo);

        Contexto ctx = new(api, client, catalogos);

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 1 — criar o processo em rascunho
        // ══════════════════════════════════════════════════════════════════════════════

        HttpResponseMessage criacao = await ExecutarPassoAsync(
            () => ctx.CriarProcessoAsync($"PS Rico E2E {sufixo}"),
            HttpStatusCode.Created, "criar processo", "POST /processos-seletivos");
        Guid processoId = await criacao.Content.ReadFromJsonAsync<Guid>();
        processoId.Should().NotBeEmpty();
        ctx = ctx with { ProcessoId = processoId };

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 2 — etapas (ordem de §4.2 do plano: etapas antes de tudo que as referencia)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutEtapasAsync(EtapasIniciais(), ifMatch: null),
            HttpStatusCode.NoContent, "PUT etapas", "/etapas");

        ProcessoSeletivoDto processoComEtapas = await ctx.ObterProcessoAsync();
        Guid objetivaId = processoComEtapas.Etapas.Single(e => e.Nome == "Prova Objetiva").Id;
        Guid redacaoId = processoComEtapas.Etapas.Single(e => e.Nome == "Redação").Id;
        Guid entrevistaId = processoComEtapas.Etapas.Single(e => e.Nome == "Entrevista").Id;

        // Âncora do estado inicial: sem este valor fixado, um corpus que já nascesse com o peso
        // pós-retificação (4.0000) tornaria vácua a asserção de peso no envelope depois do
        // fechamento — a diferença deixaria de provar que o PUT sob a sessão (passo 19) entrou.
        processoComEtapas.Etapas.Single(e => e.Id == objetivaId).Peso.Should().Be(
            3.5000m, "a Prova Objetiva nasce com este peso em EtapasIniciais — é o valor que a sessão editorial altera");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 3 — oferta de atendimento especializado (listas vazias — ver inventário §3:
        // o checklist só exige OfertaAtendimento não nulo, e essa é a primeira prova HTTP da rota)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutOfertaAtendimentoAsync([], [], []),
            HttpStatusCode.NoContent, "PUT oferta-atendimento", "/oferta-atendimento");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 4 — distribuição de vagas: uma oferta Lei 12.711, as 8 modalidades federais + AC
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutDistribuicaoVagasAsync(),
            HttpStatusCode.NoContent, "PUT distribuicao-vagas", "/distribuicao-vagas");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 5 — bônus regional
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutBonusRegionalAsync(),
            HttpStatusCode.NoContent, "PUT bonus-regional", "/bonus-regional");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 6 — critérios de desempate (referenciam a etapa Objetiva, já com Id real)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutCriteriosDesempateAsync(objetivaId),
            HttpStatusCode.NoContent, "PUT criterios-desempate", "/criterios-desempate");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 7 — classificação (regras de eliminação referenciam a mesma etapa Objetiva)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutClassificacaoAsync(objetivaId),
            HttpStatusCode.NoContent, "PUT classificacao", "/classificacao");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 8 — cronograma de fases (resolve FaseCanonica/TipoBanca do catálogo semeado)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutCronogramaFasesAsync(),
            HttpStatusCode.NoContent, "PUT cronograma-fases", "/cronograma-fases");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 9 — fatos coletados (COR_RACA de seleção única — completude semântica do DoD;
        // BAIXA_RENDA gated por COR_RACA=PRETA)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutFatosColetadosAsync(),
            HttpStatusCode.NoContent, "PUT fatos-coletados", "/fatos-coletados");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 10 — regras de derivação (MODALIDADE: AC âncora, LI_PPI quando COR_RACA=PRETA)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutRegrasDerivacaoAsync(),
            HttpStatusCode.NoContent, "PUT regras-derivacao", "/regras-derivacao");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 11 — cascata de remanejamento (matriz 8×7, fallback AC — INV-CASCATA exige
        // cobrir as 8 modalidades SegueCascata da distribuição do passo 4)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutCascataRemanejamentoAsync(),
            HttpStatusCode.NoContent, "PUT cascata-remanejamento", "/cascata-remanejamento");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 12 — documentos exigidos (array vazio — satisfaz vacuamente o item de
        // conformidade "base legal das exigências documentais"; ver inventário §12)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutDocumentosExigidosAsync([]),
            HttpStatusCode.NoContent, "PUT documentos-exigidos", "/documentos-exigidos");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 13 — formulário (rota em FormularioInscricaoController, NÃO no controller
        // principal — plano §4.2)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutFormularioAsync("Formulário de Inscrição — PS Rico E2E", "Declaro que as informações prestadas são verdadeiras.", ifMatch: null),
            HttpStatusCode.NoContent, "PUT formulario", "admin/processos-seletivos/{id}/formulario");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 14 — divulgação (a dimensão que a #563 entregou por último nesta cascata)
        // ══════════════════════════════════════════════════════════════════════════════

        await ExecutarPassoAsync(
            () => ctx.PutDivulgacaoAsync(["numero_inscricao", "nome_abreviado"], justificativa: null, ifMatch: null),
            HttpStatusCode.NoContent, "PUT divulgacao", "/divulgacao");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 15 — GET /conformidade: projeção HTTP do checklist, não gate independente
        // (plano §4.4) — lista não vazia, sem duplicatas, todos ok
        // ══════════════════════════════════════════════════════════════════════════════

        HttpResponseMessage conformidadeResp = await ExecutarPassoAsync(
            () => ctx.ObterConformidadeAsync(),
            HttpStatusCode.OK, "GET conformidade", "/conformidade");
        ConformidadeProcessoSeletivoDto conformidade = (await conformidadeResp.Content
            .ReadFromJsonAsync<ConformidadeProcessoSeletivoDto>())!;
        conformidade.ProcessoSeletivoId.Should().Be(
            processoId, "a coleção compartilha o Postgres com outros processos conformes — sem amarrar o ID, uma regressão no handler que ignorasse a rota devolveria a conformidade de outro processo");

        conformidade.Itens.Should().NotBeEmpty("uma lista vazia daria falso verde por AllSatisfy trivial");
        conformidade.Itens.Select(i => i.Item).Should().OnlyHaveUniqueItems("cada item do checklist aparece uma única vez");
        // issue #1092: o checklist passou a ser bicondicional com os QUATRO gates estruturais
        // (antes só projetava PendenciaDeConformidade) — a publicação no passo 16 só devolve 204
        // porque os quatro gates já estão satisfeitos, então o corpus rico continua OnlyContain(Ok):
        // a ampliação não pode criar falso vermelho no caminho HTTP completo.
        conformidade.Itens.Should().OnlyContain(i => i.Ok, "o processo está completo — nenhum item deveria estar pendente");
        conformidade.Itens.Select(i => i.Item).Should().BeEquivalentTo(
            [
                // ── PendenciaDeConformidade (itens estruturais originais) ──
                "Atendimento especializado",
                "Distribuição de vagas",
                "Classificação",
                "Cronograma de fases",
                "Base legal das exigências documentais",
                "Divisor da média (fórmula local)",
                "Cascata de remanejamento",
                // ── PendenciaDoCronograma ──
                "Cronograma: fase que agrupa etapas só quando há etapa pontuada",
                "Cronograma: etapa pontuada tem fase que agrupa etapas",
                "Cronograma: inscrição própria tem fase que coleta inscrição",
                "Cronograma: vagas ofertadas têm fase que produz resultado",
                // ── PendenciaDaCascata, detalhamento por razão ──
                "Cascata: modalidade SegueCascata usa a regra de distribuição federal",
                "Cascata: origem SegueCascata declarada na cascata de remanejamento",
                "Cascata: fallback e destinos resolvíveis nas modalidades ofertadas",
                "Cascata: origem declarada corresponde a modalidade SegueCascata ofertada",
                "Cascata: destino declarado corresponde a modalidade ofertada",
                // ── PendenciaPreCanonicalizacao ──
                "Exigência documental: sem CONDICIONAL vazia que determina resultado",
                "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (exigência)",
                "Consequência de indeferimento: coerente com a ação da vaga (exigência)",
                "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (grupo)",
                "Consequência de indeferimento: coerente com a ação da vaga (grupo)",
                "Referência temporal de fatos: configurada quando há gatilho por faixa etária",
                "Referência temporal de fatos: fase âncora pertence ao cronograma",
                "Referência temporal de fatos: extremo da fase âncora definido",
                "Referência temporal de fatos: fase de coleta com Fim definido para FIM_INSCRICAO",
                "Regras de derivação: fatos citados existem no processo",
                "Regras de derivação: código contribuído pertence ao domínio ofertado",
                "Grafo de dependência conjunto: sem ciclo",
            ],
            "o conjunto exato de itens que este corpus produz — remover um item do checklist, ou acrescentar um item indevido marcado ok, muda este conjunto");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 16 — publicar. A publicação em si é 204; o ato ainda NÃO existe em Publicações
        // neste instante (ADR-0108, fila durável) — é o que o passo 17 confirma.
        // ══════════════════════════════════════════════════════════════════════════════

        Guid documentoV1 = await SemearDocumentoConfirmadoAsync(api, processoId);
        await ExecutarPassoAsync(
            () => ctx.PublicarAsync(documentoV1, numero: "042/2026", tipoAtoCodigo: "EDITAL_ABERTURA"),
            HttpStatusCode.NoContent, "POST publicacao", "/publicacao");

        Guid atoV1Id = await ObterAtoCriadorUnicoAsync(api, processoId);

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 17 — BARREIRA CAUSAL: confirmar o ato V1 registrado em Publicações antes de
        // abrir a sessão (plano §4.5) — "publicou" e "o ato existe" são afirmações diferentes.
        // ══════════════════════════════════════════════════════════════════════════════

        AtoNormativo atoV1 = await EsperaDeAtoRegistrado.AguardarAsync(api, atoV1Id, "ato V1 (abertura)", processoId);
        atoV1.TipoCodigo.Should().Be("EDITAL_ABERTURA");

        // Checkpoint de ligação (plano §4.5): a identidade/hash de V1, gravados agora — o
        // fechamento (passo 21) vai reconferir que eles não mudaram um byte.
        (string hashV1, Guid versaoV1Id) = await LerIdentidadeDaVersaoAsync(api, atoV1Id);

        // O vínculo cross-módulo é provado pela IDENTIDADE da versão que o ato guarda por
        // valor, não pela existência dela: um mapeamento que gravasse a versão errada — ou uma
        // versão obsoleta de outro processo — passaria por uma asserção de nulidade.
        atoV1.VersaoInvocada.Should().NotBeNull("o ato guarda por valor a versão que ele governa (ADR-0075/0061)");
        atoV1.VersaoInvocada!.Id.Should().Be(versaoV1Id, "o ato de abertura governa exatamente V1");
        atoV1.VersaoInvocada.Hash.Should().Be(hashV1, "o hash guardado em Publicações é o mesmo que Seleção congelou para V1");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 18 — abrir a SESSÃO editorial (não o atalho atômico) — captura o ETag da abertura
        // ══════════════════════════════════════════════════════════════════════════════

        HttpResponseMessage abertura = await ExecutarPassoAsync(
            () => ctx.AbrirRetificacaoAsync("Correção do peso da prova objetiva"),
            HttpStatusCode.Created, "POST retificacao-em-curso (abrir)", "/retificacao-em-curso");
        string etagAbertura = LerETag(abertura, "abrir sessão");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 19 — PUT de uma dimensão SOB a sessão, com o If-Match da abertura — captura o
        // ETag NOVO. Chave nova por requisição (plano §4.0) — nunca reutilizar a Idempotency-Key.
        // ══════════════════════════════════════════════════════════════════════════════

        object[] etapasAlteradas = EtapasComPesoAlterado(objetivaId, redacaoId, entrevistaId);
        HttpResponseMessage mutacaoSobSessao = await ExecutarPassoAsync(
            () => ctx.PutEtapasAsync(etapasAlteradas, etagAbertura),
            HttpStatusCode.NoContent, "PUT etapas sob sessão", "/etapas");
        string etagNovo = LerETag(mutacaoSobSessao, "PUT etapas sob sessão");
        etagNovo.Should().NotBe(etagAbertura, "toda mutação aceita sob a sessão incrementa a revisão, e o controller devolve o tag novo");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 20 — fechar a sessão com o ETag NOVO (não o da abertura — plano §4.0: fechar
        // com o tag velho devolveria 412)
        // ══════════════════════════════════════════════════════════════════════════════

        Guid documentoV2 = await SemearDocumentoConfirmadoAsync(api, processoId);
        await ExecutarPassoAsync(
            () => ctx.FecharRetificacaoAsync(documentoV2, numero: "042/2026-R", tipoAtoCodigo: "EDITAL_RETIFICACAO", etagNovo),
            HttpStatusCode.NoContent, "POST retificacao-em-curso/fechamento", "/retificacao-em-curso/fechamento");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 21 — identidade das versões pelo AtoCriadorId (plano §4.5, bloqueante): V2
        // retifica V1, nunca "qualquer ato do processo". V1 permanece intacta (append-only).
        // ══════════════════════════════════════════════════════════════════════════════

        (Guid atoV2Id, Guid atoV2RetificaId, string hashV1AposFechamento, Guid versaoV2Id, string hashV2) =
            await LerVersoesAposFechamentoAsync(api, processoId);

        atoV2Id.Should().NotBe(atoV1Id, "o fechamento cria um ato novo — nunca reaproveita o de abertura");
        atoV2RetificaId.Should().Be(atoV1Id, "V2.AtoCriadorRetificaId aponta para o ato que ela retifica: V1");
        hashV1AposFechamento.Should().Be(hashV1, "a versão anterior é imutável — o fechamento não muta o passado (append-only)");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 22 — confirmar o ato V2 em Publicações, pela identidade EXATA de V2 (não
        // "qualquer ato do processo" — é a barreira que o plano nomeia como bloqueante)
        // ══════════════════════════════════════════════════════════════════════════════

        AtoNormativo atoV2 = await EsperaDeAtoRegistrado.AguardarAsync(
            api, atoV2Id, "ato V2 (fechamento da sessão)", processoId, atoPredecessorId: atoV1Id);

        atoV2.AtoRetificadoId.Should().Be(atoV1Id, "no ato registrado em Publicações, a relação de retificação espelha a de Seleção");
        atoV2.TipoCodigo.Should().Be("EDITAL_RETIFICACAO");
        atoV2.VersaoInvocada.Should().NotBeNull("o ato guarda por valor a versão que ele governa (ADR-0075/0061)");
        atoV2.VersaoInvocada!.Id.Should().Be(versaoV2Id, "o ato retificador governa exatamente V2 — nunca V1 nem uma versão obsoleta");
        atoV2.VersaoInvocada.Hash.Should().Be(hashV2, "o hash guardado em Publicações é o mesmo que Seleção congelou para V2");
        hashV2.Should().NotBe(hashV1, "V2 congela outra configuração — se os hashes coincidissem, a asserção de identidade acima seria vácua");

        // ══════════════════════════════════════════════════════════════════════════════
        // Passo 23 — GET /snapshot-vigente == versão 2 (a versão vigente reflete o fechamento)
        // ══════════════════════════════════════════════════════════════════════════════

        HttpResponseMessage snapshotResp = await ExecutarPassoAsync(
            () => ctx.ObterSnapshotVigenteAsync(),
            HttpStatusCode.OK, "GET snapshot-vigente", "/snapshot-vigente");
        SnapshotVigenteDto snapshotVigente = (await snapshotResp.Content.ReadFromJsonAsync<SnapshotVigenteDto>())!;

        snapshotVigente.AtoId.Should().Be(atoV2Id, "o snapshot vigente aponta para o ato que o governa — V2, depois do fechamento");

        // A diferença de hash sozinha não prova que a edição entrou: toda retificação
        // acrescenta o bloco `retificacao` ao envelope, então o hash muda mesmo que o peso
        // enviado no passo 19 tivesse sido ignorado. A prova de que a edição entrou é o valor
        // do peso dentro da configuração congelada, casado pelo id da etapa — nunca por
        // posição no array, que a política de ordenação pode mudar.
        JsonObject etapaObjetivaCongelada = LocalizarEtapaPorId(snapshotVigente.Configuracao, objetivaId);
        etapaObjetivaCongelada["peso"]!.GetValue<string>().Should().Be(
            "4.0000", "V2 congela a configuração EDITADA sob a sessão — o peso da Prova Objetiva enviado no passo 19");
        snapshotVigente.HashConfiguracao.Should().NotBe(hashV1, "V2 é uma versão congelada distinta de V1 — consequência de qualquer retificação, provada aqui só como reforço da asserção de peso acima");

        await using AsyncServiceScope scopeFinal = api.Services.CreateAsyncScope();
        SelecaoDbContext dbFinal = scopeFinal.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        int totalDeVersoes = await dbFinal.VersoesConfiguracao.AsNoTracking()
            .CountAsync(v => v.ProcessoSeletivoId == processoId);
        totalDeVersoes.Should().Be(2, "publicação (V1) + fechamento da sessão (V2) — nada mais congelou uma versão");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Checkpoint — valida a resposta IMEDIATAMENTE, nomeando fase/rota/status/ProblemDetails
    // ══════════════════════════════════════════════════════════════════════════════

    private static async Task<HttpResponseMessage> ExecutarPassoAsync(
        Func<Task<HttpResponseMessage>> acao, HttpStatusCode esperado, string passo, string rota)
    {
        HttpResponseMessage resposta = await acao().ConfigureAwait(false);
        if (resposta.StatusCode != esperado)
        {
            string corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            resposta.StatusCode.Should().Be(
                esperado,
                $"passo '{passo}' ({rota}) esperava {(int)esperado} {esperado} e recebeu " +
                $"{(int)resposta.StatusCode} {resposta.StatusCode}. ProblemDetails: {corpo}");
        }

        return resposta;
    }

    private static string LerETag(HttpResponseMessage resposta, string passo)
    {
        resposta.Headers.ETag.Should().NotBeNull($"passo '{passo}': a resposta de uma sessão editorial carrega o ETag");
        return resposta.Headers.ETag!.ToString();
    }

    /// <summary>
    /// Localiza, dentro do bloco <c>etapas</c> do envelope congelado, o item cujo <c>id</c>
    /// bate com <paramref name="etapaId"/> — casamento por identidade, nunca por posição no
    /// array (<c>SnapshotPublicacaoCanonicalizer.SerializarEtapas</c> reordena por conteúdo
    /// quando duas etapas empatam em <c>Ordem</c>).
    /// </summary>
    private static JsonObject LocalizarEtapaPorId(JsonNode configuracao, Guid etapaId) =>
        configuracao["etapas"]!.AsArray()
            .Select(static no => no!.AsObject())
            .Single(etapa => etapa["id"]!.GetValue<Guid>() == etapaId);

    // ══════════════════════════════════════════════════════════════════════════════
    // Payloads das dimensões — um método por dimensão, na ordem topológica do plano §4.2
    // ══════════════════════════════════════════════════════════════════════════════

    private static readonly Guid TipoEtapaProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    private static readonly Guid TipoEtapaRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");
    private static readonly Guid TipoEtapaEntrevistaOrigemId = new("019fee1e-7000-7000-8000-000000000003");

    private static object[] EtapasIniciais() =>
    [
        new { nome = "Prova Objetiva", carater = (int)CaraterEtapa.Ambas, tipoEtapaOrigemId = TipoEtapaProvaObjetivaOrigemId, peso = 3.5000m, notaMinima = 40.0000m, ordem = 1 },
        new { nome = "Redação", carater = (int)CaraterEtapa.Classificatoria, tipoEtapaOrigemId = TipoEtapaRedacaoOrigemId, peso = 2.2500m, notaMinima = (decimal?)null, ordem = 2 },
        new { nome = "Entrevista", carater = (int)CaraterEtapa.Eliminatoria, tipoEtapaOrigemId = TipoEtapaEntrevistaOrigemId, peso = (decimal?)null, notaMinima = 60.0000m, ordem = 3 },
    ];

    /// <summary>
    /// A mesma coleção, com o Id de cada etapa ECOADO (reconciliação in-place —
    /// <c>DefinirEtapasCommandHandler.cs:64-77</c>) e o peso da Objetiva alterado — é a
    /// dimensão que muda sob a sessão editorial (passo 19).
    /// </summary>
    private static object[] EtapasComPesoAlterado(Guid objetivaId, Guid redacaoId, Guid entrevistaId) =>
    [
        new { id = objetivaId, nome = "Prova Objetiva", carater = (int)CaraterEtapa.Ambas, tipoEtapaOrigemId = TipoEtapaProvaObjetivaOrigemId, peso = 4.0000m, notaMinima = 40.0000m, ordem = 1 },
        new { id = redacaoId, nome = "Redação", carater = (int)CaraterEtapa.Classificatoria, tipoEtapaOrigemId = TipoEtapaRedacaoOrigemId, peso = 2.2500m, notaMinima = (decimal?)null, ordem = 2 },
        new { id = entrevistaId, nome = "Entrevista", carater = (int)CaraterEtapa.Eliminatoria, tipoEtapaOrigemId = TipoEtapaEntrevistaOrigemId, peso = (decimal?)null, notaMinima = 60.0000m, ordem = 3 },
    ];

    // ══════════════════════════════════════════════════════════════════════════════
    // Infra do cenário — Contexto (mesmo papel do "Ciclo"/"Contexto" dos demais testes desta
    // pasta), catálogos e leituras diretas de banco para os checkpoints de identidade
    // ══════════════════════════════════════════════════════════════════════════════

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, CatalogosSemeados Catalogos)
    {
        private const string Rota = "/api/selecao/processos-seletivos";

        public Guid ProcessoId { get; init; }

        public Task<HttpResponseMessage> CriarProcessoAsync(string nome) => EnviarAsync(
            HttpMethod.Post, Rota,
            new
            {
                nome,
                tipoProcessoOrigemId = Catalogos.TipoProcessoId,
                origemCandidatos = 1, // OrigemCandidatos.InscricaoPropria
                unidadeAdministradoraOrigemId = Catalogos.UnidadeId,
            },
            ifMatch: null);

        public Task<HttpResponseMessage> PutEtapasAsync(object[] etapas, string? ifMatch) =>
            EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/etapas", etapas, ifMatch);

        public Task<HttpResponseMessage> PutOfertaAtendimentoAsync(
            IReadOnlyList<Guid> condicaoIds, IReadOnlyList<Guid> recursoIds, IReadOnlyList<Guid> tipoDeficienciaIds) =>
            EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/oferta-atendimento",
                new { condicaoIds, recursoIds, tipoDeficienciaIds }, ifMatch: null);

        public Task<HttpResponseMessage> PutDistribuicaoVagasAsync()
        {
            object[] distribuicao =
            [
                new
                {
                    ofertaCursoId = Catalogos.OfertaCursoId,
                    voBase = 60,
                    pr = 0.7500m,
                    regraDistribuicaoCodigo = RegraDistribuicaoVagasCodigo.Lei12711,
                    regraDistribuicaoVersao = "v1",
                    regraAjusteCodigo = RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico,
                    regraAjusteVersao = "v1",
                    referenciaReservaDemograficaId = Catalogos.ReferenciaReservaDemograficaId,
                    modalidadeIds = Catalogos.ModalidadeIdPorCodigo.Values.ToArray(),
                    quadro = Array.Empty<object>(),
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/distribuicao-vagas", distribuicao, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutBonusRegionalAsync() => EnviarAsync(
            HttpMethod.Put, $"{Rota}/{ProcessoId}/bonus-regional",
            new
            {
                regraCodigo = RegraBonusCodigo.Multiplicativo,
                regraVersao = "v1",
                fator = 1.2000m,
                teto = 95.5000m,
                municipioConvenio = "Marabá",
                baseLegal = "Res. Unifesspa 414/2020",
            },
            ifMatch: null);

        public Task<HttpResponseMessage> PutCriteriosDesempateAsync(Guid objetivaId)
        {
            object[] criterios =
            [
                new { ordem = 1, regraCodigo = CriterioDesempateCodigo.Idoso, regraVersao = "v1", idadeMinima = 60, etapaRef = (Guid?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
                new { ordem = 2, regraCodigo = CriterioDesempateCodigo.MaiorNotaEtapa, regraVersao = "v1", etapaRef = (Guid?)objetivaId, idadeMinima = (int?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
                new { ordem = 3, regraCodigo = CriterioDesempateCodigo.MaiorIdade, regraVersao = "v1", etapaRef = (Guid?)null, idadeMinima = (int?)null, fato = (string?)null, @operador = (string?)null, valor = (string?)null },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/criterios-desempate", criterios, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutClassificacaoAsync(Guid objetivaId)
        {
            object[] regrasEliminacao =
            [
                new { regraCodigo = RegraEliminacaoCodigo.ElimNotaMinimaEtapa, regraVersao = "v1", etapaRef = (Guid?)objetivaId, notaMinima = (decimal?)45.0000m, minimo = (decimal?)null },
                new { regraCodigo = RegraEliminacaoCodigo.ElimCorteRedacao, regraVersao = "v1", etapaRef = (Guid?)null, notaMinima = (decimal?)null, minimo = (decimal?)400.0000m },
                new { regraCodigo = RegraEliminacaoCodigo.ElimZeroEmArea, regraVersao = "v1", etapaRef = (Guid?)null, notaMinima = (decimal?)null, minimo = (decimal?)null },
            ];

            return EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/classificacao",
                new
                {
                    regraCalculoCodigo = RegraCalculoCodigo.FormulaMediaPonderada,
                    regraCalculoVersao = "v1",
                    regraArredondamentoCodigo = RegraArredondamentoCodigo.PrecisaoArredondarCima,
                    regraArredondamentoVersao = "v1",
                    casasArredondamento = 3,
                    regraOrdemAlocacaoCodigo = RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04,
                    regraOrdemAlocacaoVersao = "v1",
                    nOpcoesAlocacao = 2,
                    regrasEliminacao,
                    baseadoEmEnem = true,
                },
                ifMatch: null);
        }

        public Task<HttpResponseMessage> PutCronogramaFasesAsync()
        {
            object[] fases =
            [
                new
                {
                    ordem = 1,
                    faseCanonicaId = Catalogos.FaseInscricaoId,
                    inicio = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
                    fim = new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
                    atoProduzidoCodigo = (string?)null,
                    tiposBancaIds = Array.Empty<Guid>(),
                    regraRecurso = (object?)null,
                },
                new
                {
                    ordem = 2,
                    faseCanonicaId = Catalogos.FaseResultadoPreliminarId,
                    inicio = new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
                    fim = new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
                    // A fase que produz resultado tem de declarar o ato que produz
                    // (FaseCronograma.AtoProduzidoObrigatorio) — o tipo é semeado à parte de
                    // TiposDeAtoSeeder, que só cobre os dois atos da sessão editorial.
                    atoProduzidoCodigo = CodigoAtoResultadoPreliminar,
                    tiposBancaIds = new[] { Catalogos.TipoBancaId },
                    regraRecurso = (object?)null,
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/cronograma-fases", fases, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutFatosColetadosAsync()
        {
            object[] fatos =
            [
                new { fatoCodigo = "COR_RACA", ordem = 0, rotulo = "Cor ou raça", tipoRenderizacao = TipoRenderizacaoCodigo.SelecaoUnica, obrigatorio = true, precondicao = (object?)null },
                new
                {
                    fatoCodigo = "BAIXA_RENDA",
                    ordem = 1,
                    rotulo = "Baixa renda",
                    tipoRenderizacao = TipoRenderizacaoCodigo.Booleano,
                    obrigatorio = false,
                    precondicao = new[] { new[] { new { fato = "COR_RACA", @operador = OperadorCodigo.Igual, valor = "PRETA" } } },
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/fatos-coletados", fatos, ifMatch: null);
        }

        public Task<HttpResponseMessage> PutRegrasDerivacaoAsync()
        {
            object[] configuracoes =
            [
                new
                {
                    codigoFato = "MODALIDADE",
                    regras = new object[]
                    {
                        new { ordem = 0, contribui = "AC", quando = (object?)null },
                        new
                        {
                            ordem = 1,
                            contribui = "LI_PPI",
                            quando = new[] { new[] { new { fato = "COR_RACA", @operador = OperadorCodigo.Igual, valor = "PRETA" } } },
                        },
                    },
                },
            ];

            return EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/regras-derivacao", configuracoes, ifMatch: null);
        }

        /// <summary>
        /// Matriz 8×7, fallback AC — a MESMA forma do <c>esquema_args</c> congelado da regra
        /// <c>REMANEJ-CASCATA-LEI-12711</c> v1 (<c>RegraCatalogoSeed.cs:169-183</c>): para cada
        /// origem federal, os destinos são as outras 7, na ordem declarada em
        /// <see cref="ModalidadesFederaisLei12711.Codigos"/> — sem permutar, porque o handler
        /// compara célula a célula contra o catálogo, não só a forma.
        /// </summary>
        public Task<HttpResponseMessage> PutCascataRemanejamentoAsync()
        {
            IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
            List<object> destinos = [];
            foreach (string origem in origens)
            {
                string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
                for (int i = 0; i < destinosDaOrigem.Length; i++)
                {
                    destinos.Add(new { modalidadeOrigemCodigo = origem, ordem = i + 1, modalidadeDestinoCodigo = destinosDaOrigem[i] });
                }
            }

            return EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/cascata-remanejamento",
                new
                {
                    regraCodigo = RegraRemanejamentoCodigo.Cascata,
                    regraVersao = "v1",
                    fallbackCodigo = ModalidadesFederaisLei12711.Ac,
                    destinos,
                },
                ifMatch: null);
        }

        public Task<HttpResponseMessage> PutDocumentosExigidosAsync(object[] raizes) =>
            EnviarAsync(HttpMethod.Put, $"{Rota}/{ProcessoId}/documentos-exigidos", raizes, ifMatch: null);

        public Task<HttpResponseMessage> PutFormularioAsync(string titulo, string termoAceiteTexto, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{ProcessoId}/formulario",
                new { titulo, termoAceiteTexto }, ifMatch);

        public Task<HttpResponseMessage> PutDivulgacaoAsync(IReadOnlyList<string>? camposPublicos, string? justificativa, string? ifMatch) =>
            EnviarAsync(
                HttpMethod.Put, $"{Rota}/{ProcessoId}/divulgacao",
                new { camposPublicos, justificativa }, ifMatch);

        public async Task<ProcessoSeletivoDto> ObterProcessoAsync()
        {
            HttpResponseMessage resposta = await EnviarAsync(
                HttpMethod.Get, $"{Rota}/{ProcessoId}", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.processo-seletivo.v1+json").ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, "GET do recurso, usado para descobrir os Ids gerados pelo servidor");
            return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>().ConfigureAwait(false))!;
        }

        public Task<HttpResponseMessage> ObterConformidadeAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/conformidade", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.conformidade-processo-seletivo.v1+json");

        public Task<HttpResponseMessage> PublicarAsync(Guid documentoEditalId, string numero, string tipoAtoCodigo) =>
            EnviarAsync(
                HttpMethod.Post, $"{Rota}/{ProcessoId}/publicacao",
                new
                {
                    numero,
                    periodoInscricaoInicio = "2026-03-02",
                    periodoInscricaoFim = "2026-04-15",
                    documentoEditalId,
                    ato = NovoAto(tipoAtoCodigo),
                },
                ifMatch: null);

        public Task<HttpResponseMessage> AbrirRetificacaoAsync(string motivo) =>
            EnviarAsync(HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso", new { motivo }, ifMatch: null);

        public Task<HttpResponseMessage> FecharRetificacaoAsync(Guid documentoEditalId, string numero, string tipoAtoCodigo, string ifMatch) =>
            EnviarAsync(
                HttpMethod.Post, $"{Rota}/{ProcessoId}/retificacao-em-curso/fechamento",
                new
                {
                    numero,
                    periodoInscricaoInicio = "2026-03-02",
                    periodoInscricaoFim = "2026-04-15",
                    documentoEditalId,
                    ato = NovoAto(tipoAtoCodigo),
                },
                ifMatch);

        public Task<HttpResponseMessage> ObterSnapshotVigenteAsync() =>
            EnviarAsync(HttpMethod.Get, $"{Rota}/{ProcessoId}/snapshot-vigente", null, ifMatch: null, idempotente: false,
                accept: "application/vnd.uniplus.snapshot-vigente-processo-seletivo.v1+json");

        private static object NovoAto(string tipoAtoCodigo) => new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataPublicacao = "2026-03-01",
            assinante = "Diretor do CEPS",
            tipoAtoCodigo,
        };

        private async Task<HttpResponseMessage> EnviarAsync(
            HttpMethod metodo, string rota, object? corpo, string? ifMatch, bool idempotente = true, string? accept = null)
        {
            using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
            if (corpo is not null)
            {
                request.Content = JsonContent.Create(corpo);
            }

            // Leituras versionadas exigem o vendor media type (ADR-0028) — sem ele o servidor
            // devolve 406, e o teste "falharia" por um motivo que nada tem a ver com o que ele
            // quer provar (plano §4.0).
            if (accept is not null)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");

            // Chave de idempotência NOVA em toda mutação (plano §4.0) — reutilizar transformaria
            // a segunda chamada em replay e mascararia a dimensão realmente enviada.
            if (idempotente)
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            }

            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }

    /// <summary>Os ids dos catálogos semeados por <see cref="SemearCatalogosRicosAsync"/>.</summary>
    private sealed record CatalogosSemeados(
        Guid UnidadeId,
        Guid OfertaCursoId,
        Guid ReferenciaReservaDemograficaId,
        Guid FaseInscricaoId,
        Guid FaseResultadoPreliminarId,
        Guid TipoBancaId,
        Guid TipoProcessoId,
        IReadOnlyDictionary<string, Guid> ModalidadeIdPorCodigo);

    /// <summary>
    /// Semeia, direto pelo <c>DbContext</c> de cada módulo (mesmo Postgres compartilhado pela
    /// <see cref="CascadingFixture"/>), os catálogos que os handlers de Seleção resolvem e que não
    /// têm <c>HasData</c>: Unidade administradora (Organização Institucional), Curso + LocalOferta
    /// + OfertaCurso + ReferenciaReservaDemografica (Configuração), FaseCanonica + TipoBanca
    /// (Configuração). As 10 <see cref="Modalidade"/> federais já vêm semeadas por migration
    /// (<see cref="ModalidadeSeed"/>) — só referenciadas aqui pelo código.
    /// </summary>
    private static async Task<CatalogosSemeados> SemearCatalogosRicosAsync(CascadingApiFactory api, string sufixo)
    {
        Guid unidadeId;
        await using (AsyncServiceScope scopeOrg = api.Services.CreateAsyncScope())
        {
            OrganizacaoInstitucionalDbContext org = scopeOrg.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
            Unidade unidade = Unidade.Criar(
                nome: "Centro de Processos Seletivos E2E",
                alias: null,
                slug: Slug.From($"ceps-e2e-{sufixo}").Value!,
                sigla: $"CEPS{sufixo}",
                codigo: $"CEPS-{sufixo}",
                unidadeSuperiorId: null,
                tipo: TipoUnidade.Centro,
                unidadeAcademica: true,
                vigenciaInicio: new DateOnly(2020, 1, 1),
                vigenciaFim: null).Value!;
            org.Unidades.Add(unidade);
            await org.SaveChangesAsync().ConfigureAwait(false);
            unidadeId = unidade.Id;
        }

        Guid ofertaCursoId;
        Guid referenciaDemograficaId;
        Guid faseInscricaoId;
        Guid faseResultadoPreliminarId;
        Guid tipoBancaId;
        Guid tipoProcessoId;
        await using (AsyncServiceScope scopeConfig = api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext config = scopeConfig.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

            Curso curso = Curso.Criar($"MED{sufixo}", "Medicina", "Bacharelado", "Graduação", null).Value!;
            LocalOferta local = LocalOferta.Criar(
                TipoLocalOferta.CampusSede,
                campusResponsavelId: null,
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA",
                cidadeOrigem: Unifesspa.UniPlus.Kernel.Domain.Cidades.ReferenciaCidadeGeo.OrigemGeoApi,
                cidadeDisplayAtualizadoEm: DateTimeOffset.UtcNow,
                endereco: null,
                codigoEmec: null).Value!;
            config.Cursos.Add(curso);
            config.LocaisOferta.Add(local);
            await config.SaveChangesAsync().ConfigureAwait(false);

            Result<UnidadeOfertante> unidadeOfertanteResult = UnidadeOfertante.Criar(
                unidadeId, $"CEPS{sufixo}", "Centro de Processos Seletivos E2E", nameof(TipoUnidade.Centro));
            unidadeOfertanteResult.IsSuccess.Should().BeTrue(unidadeOfertanteResult.Error?.Message);
            Result<OfertaCurso> ofertaResult = OfertaCurso.Criar(
                curso.Id, local.Id, unidadeOfertanteResult.Value!,
                programaDeOferta: "REGULAR", formatoPedagogico: null, turno: null,
                eMecCodigo: null, codigoSga: null, vagasAnuaisAutorizadas: null,
                baseLegal: null, atoAutorizacaoMec: null);
            ofertaResult.IsSuccess.Should().BeTrue(ofertaResult.Error?.Message);
            OfertaCurso oferta = ofertaResult.Value!;

            Result<ReferenciaReservaDemografica> referenciaResult = ReferenciaReservaDemografica.Criar(
                "Censo IBGE 2022", 78.55m, 1.20m, 8.40m, "Lei 12.711/2012 art. 3º");
            referenciaResult.IsSuccess.Should().BeTrue(referenciaResult.Error?.Message);
            ReferenciaReservaDemografica referencia = referenciaResult.Value!;

            Result<FaseCanonica> faseInscricaoResult = FaseCanonica.Criar(
                "INSCRICAO", "Inscrição", null, "CRCA",
                agrupaEtapas: false, permiteComplementacao: false, baseLegal: null,
                produzResultado: false, resultadoDefinitivo: false, coletaInscricao: true,
                origemData: "PROPRIA");
            faseInscricaoResult.IsSuccess.Should().BeTrue(faseInscricaoResult.Error?.Message);
            FaseCanonica faseInscricao = faseInscricaoResult.Value!;

            // "Apenas a fase de avaliação agrupa etapas pontuadas" (FaseCanonica.Criar) — o
            // único código do catálogo fechado com essa permissão é AVALIACAO
            // (FaseCanonicaCatalogo.CodigoAvaliacao).
            Result<FaseCanonica> faseResultadoPreliminarResult = FaseCanonica.Criar(
                FaseCanonicaCatalogo.CodigoAvaliacao, "Avaliação", null, "CEPS",
                agrupaEtapas: true, permiteComplementacao: false, baseLegal: null,
                produzResultado: true, resultadoDefinitivo: false, coletaInscricao: false,
                origemData: "PROPRIA");
            faseResultadoPreliminarResult.IsSuccess.Should().BeTrue(faseResultadoPreliminarResult.Error?.Message);
            FaseCanonica faseResultadoPreliminar = faseResultadoPreliminarResult.Value!;

            Result<TipoBanca> bancaResult = TipoBanca.Criar(
                "BANCA_ANALISE_DOCUMENTAL", "Banca de análise documental", "RESULTADO_PRELIMINAR", null);
            bancaResult.IsSuccess.Should().BeTrue(bancaResult.Error?.Message);
            TipoBanca banca = bancaResult.Value!;

            config.OfertasCurso.Add(oferta);
            config.ReferenciasReservaDemografica.Add(referencia);
            config.FasesCanonicas.Add(faseInscricao);
            config.FasesCanonicas.Add(faseResultadoPreliminar);
            config.TiposBanca.Add(banca);
            await config.SaveChangesAsync().ConfigureAwait(false);

            ofertaCursoId = oferta.Id;
            referenciaDemograficaId = referencia.Id;
            faseInscricaoId = faseInscricao.Id;
            faseResultadoPreliminarId = faseResultadoPreliminar.Id;
            tipoBancaId = banca.Id;
            tipoProcessoId = await config.TiposProcesso
                .Where(tipo => tipo.Codigo == "SiSU" && tipo.Ativo)
                .Select(tipo => tipo.Id)
                .SingleAsync().ConfigureAwait(false);
        }

        Dictionary<string, Guid> modalidadeIdPorCodigo = ModalidadeSeed.Itens
            .Where(i => i.Codigo == ModalidadesFederaisLei12711.Ac || ModalidadesFederaisLei12711.Codigos.Contains(i.Codigo))
            .ToDictionary(i => i.Codigo, i => i.Id, StringComparer.Ordinal);

        return new CatalogosSemeados(
            unidadeId, ofertaCursoId, referenciaDemograficaId, faseInscricaoId, faseResultadoPreliminarId, tipoBancaId, tipoProcessoId,
            modalidadeIdPorCodigo);
    }

    /// <summary>
    /// <see cref="TiposDeAtoSeeder"/> só cobre os dois atos da sessão editorial (abertura e
    /// retificação). A fase que produz resultado (<see cref="CodigoAtoResultadoPreliminar"/>)
    /// exige seu próprio tipo vigente no catálogo de Publicações — semeado aqui, à parte, sem
    /// duplicar o seeder existente para um único código extra.
    /// </summary>
    private static async Task SemearTipoDeAtoResultadoPreliminarAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.PublicacoesDbContext db =
            scope.ServiceProvider.GetRequiredService<Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == CodigoAtoResultadoPreliminar))
        {
            return;
        }

        Result<TipoAtoPublicado> tipoResult = TipoAtoPublicado.Criar(
            CodigoAtoResultadoPreliminar,
            "Resultado preliminar",
            congelaConfiguracao: false,
            unicoPorObjeto: false,
            efeitoIrreversivel: true,
            new DateOnly(2020, 1, 1),
            vigenciaFim: null,
            baseLegal: null);
        tipoResult.IsSuccess.Should().BeTrue(tipoResult.Error?.Message);

        await db.Set<TipoAtoPublicado>().AddAsync(tipoResult.Value!).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return documento.Id;
    }

    private static async Task<Guid> ObterAtoCriadorUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    private static async Task<(string Hash, Guid VersaoId)> LerIdentidadeDaVersaoAsync(CascadingApiFactory api, Guid atoCriadorId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking()
            .SingleAsync(v => v.AtoCriadorId == atoCriadorId);
        return (versao.HashConfiguracao, versao.Id);
    }

    /// <summary>
    /// Lê as duas versões congeladas depois do fechamento e devolve, pela identidade do
    /// <c>AtoCriadorId</c> (plano §4.5) — nunca "qualquer ato do processo" — o que o fechamento
    /// produziu: o id do ato V2, o ato que ele retifica (deve ser o de V1) e o hash de V1 tal
    /// como está gravado agora (para o checkpoint de imutabilidade).
    /// </summary>
    private static async Task<(Guid AtoV2Id, Guid AtoV2RetificaId, string HashV1AposFechamento, Guid VersaoV2Id, string HashV2)> LerVersoesAposFechamentoAsync(
        CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2, "publicação (V1) + fechamento da sessão (V2)");
        VersaoConfiguracao v1 = versoes[0];
        VersaoConfiguracao v2 = versoes[1];
        v2.AtoCriadorRetificaId.Should().NotBeNull("V2 é uma retificação — ela emenda o ato criador de V1");

        return (v2.AtoCriadorId, v2.AtoCriadorRetificaId!.Value, v1.HashConfiguracao, v2.Id, v2.HashConfiguracao);
    }
}
