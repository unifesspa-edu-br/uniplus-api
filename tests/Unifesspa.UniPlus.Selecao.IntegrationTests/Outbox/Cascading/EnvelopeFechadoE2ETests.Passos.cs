namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class EnvelopeFechadoE2ETests
{
    /// <summary>O que um passo do ciclo lega aos seguintes: o contexto HTTP e as identidades que cada um descobriu.</summary>
    private sealed class Cenario(CascadingApiFactory api, string sufixo, Contexto ctx)
    {
        public CascadingApiFactory Api { get; } = api;

        public string Sufixo { get; } = sufixo;

        public Contexto Ctx { get; set; } = ctx;

        public Guid ObjetivaId { get; set; }

        public Guid RedacaoId { get; set; }

        public Guid EntrevistaId { get; set; }

        public string ResolucaoPesoAreaEnem { get; set; } = string.Empty;

        public Guid AtoV1Id { get; set; }

        public string HashV1 { get; set; } = string.Empty;

        public string EtagDaAbertura { get; set; } = string.Empty;

        public string EtagNovo { get; set; } = string.Empty;

        public Guid AtoV2Id { get; set; }

        public Guid VersaoV2Id { get; set; }

        public string HashV2 { get; set; } = string.Empty;
    }

    // Os últimos 8 hex de um GUID v7 (rand_b) são aleatórios — os PRIMEIROS 8 são o timestamp
    // em milissegundos, que só muda a cada ~65s e colidiria com LimpezaRegrasDistribuicaoMigrationTests
    // (mesma coleção sequencial, mesmo semeador) se os dois rodassem dentro da mesma janela.
    private async Task<Cenario> PrepararAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        string sufixo = Guid.CreateVersion7().ToString("N")[^8..];

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeAtoResultadoPreliminarAsync(api);
        CatalogosSemeados catalogos = await SemearCatalogosRicosAsync(api, sufixo);

        return new Cenario(api, sufixo, new Contexto(api, api.CreateClient(), catalogos));
    }

    private static async Task ConfigurarTodasAsDimensoesAsync(Cenario c)
    {
        await CriarProcessoEmRascunhoAsync(c);
        await DeclararEtapasAsync(c);
        await DeclararAtendimentoEVagasAsync(c);
        await DeclararBonusETaxaAsync(c);
        await DeclararDesempateEClassificacaoAsync(c);
        await DeclararFormularioAsync(c);
        await DeclararRegrasEDocumentosAsync(c);
    }

    private static async Task PublicarEConfirmarV1Async(Cenario c)
    {
        await PublicarV1Async(c);
        await ConfirmarAtoV1Async(c);
    }

    private static async Task RetificarSobSessaoEditorialAsync(Cenario c)
    {
        await AbrirSessaoEditorialAsync(c);
        await AlterarEtapasSobASessaoAsync(c);
        await FecharSessaoEditorialAsync(c);
    }

    private static async Task ConferirIdentidadeEAtoV2Async(Cenario c)
    {
        await ConferirIdentidadeDasVersoesAsync(c);
        await ConfirmarAtoV2Async(c);
    }

    // Passo 1 — criar o processo em rascunho.
    private static async Task CriarProcessoEmRascunhoAsync(Cenario c)
    {
        HttpResponseMessage criacao = await ExecutarPassoAsync(
            () => c.Ctx.CriarProcessoAsync($"PS Rico E2E {c.Sufixo}"),
            HttpStatusCode.Created, "criar processo", "POST /processos-seletivos");
        Guid processoId = await criacao.Content.ReadFromJsonAsync<Guid>();
        processoId.Should().NotBeEmpty();
        c.Ctx = c.Ctx with { ProcessoId = processoId };
    }

    // Passo 2 — cronograma e etapas (ordem de §4.2 do plano: etapas antes de tudo que as referencia).
    // O peso inicial da Objetiva é âncora: sem este valor fixado, um corpus que já nascesse com o peso
    // pós-retificação (4.0000) tornaria vácua a asserção de peso no envelope depois do fechamento.
    private static async Task DeclararEtapasAsync(Cenario c)
    {
        await ExecutarPassoAsync(() => c.Ctx.PutCronogramaFasesAsync(), HttpStatusCode.NoContent, "PUT cronograma-fases", "/cronograma-fases");
        await ExecutarPassoAsync(() => c.Ctx.PutEtapasAsync(EtapasIniciais(), ifMatch: null), HttpStatusCode.NoContent, "PUT etapas", "/etapas");

        ProcessoSeletivoDto processo = await c.Ctx.ObterProcessoAsync();
        c.ObjetivaId = processo.Etapas.Single(e => e.Nome == "Prova Objetiva").Id;
        c.RedacaoId = processo.Etapas.Single(e => e.Nome == "Redação").Id;
        c.EntrevistaId = processo.Etapas.Single(e => e.Nome == "Entrevista").Id;
        processo.Etapas.Single(e => e.Id == c.ObjetivaId).Peso.Should().Be(
            3.5000m, "a Prova Objetiva nasce com este peso em EtapasIniciais — é o valor que a sessão editorial altera");
    }

    // Passos 3, 4 e 4b — oferta de atendimento (listas vazias: o checklist só exige OfertaAtendimento
    // não nulo), distribuição de vagas (uma oferta Lei 12.711, as 8 modalidades federais + AC) e
    // número de opções de curso, conferido contra a única oferta da distribuição.
    private static async Task DeclararAtendimentoEVagasAsync(Cenario c)
    {
        await ExecutarPassoAsync(() => c.Ctx.PutOfertaAtendimentoAsync([], [], []), HttpStatusCode.NoContent, "PUT oferta-atendimento", "/oferta-atendimento");
        HttpResponseMessage resposta = await ExecutarPassoAsync(
            () => c.Ctx.PutDistribuicaoVagasAsync(), HttpStatusCode.OK, "PUT distribuicao-vagas", "/distribuicao-vagas");
        IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> persistida =
            (await resposta.Content.ReadFromJsonAsync<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>())!;
        persistida.Should().ContainSingle();
        persistida.Single().Quadro.Should().NotBeEmpty("o quadro é calculado pela Lei 12.711 na mesma operação que persiste (issue #848/#1283)");
        await ExecutarPassoAsync(() => c.Ctx.PutOpcoesDeCursoAsync(), HttpStatusCode.NoContent, "PUT opcoes-de-curso", "/opcoes-de-curso");
    }

    // Passos 5 e 5b — bônus regional e taxa de inscrição: publicar sem declarar cobrança é
    // recusado, e o corpus declara "não cobra".
    private static async Task DeclararBonusETaxaAsync(Cenario c)
    {
        Guid baseLegalId = await SemearBaseLegalBonusRegionalAsync(c.Api);

        await ExecutarPassoAsync(() => c.Ctx.PutBonusRegionalAsync(baseLegalId), HttpStatusCode.NoContent, "PUT bonus-regional", "/bonus-regional");
        await ExecutarPassoAsync(() => c.Ctx.PutTaxaInscricaoAsync(), HttpStatusCode.NoContent, "PUT taxa-inscricao", "/taxa-inscricao");
    }

    // Passos 6, 7 e 7b — desempate e regras de eliminação referenciam a etapa Objetiva, já com Id real;
    // a classificação baseada em ENEM exige ao menos uma edição declarada para o fato EDICAO_ENEM.
    private static async Task DeclararDesempateEClassificacaoAsync(Cenario c)
    {
        await ExecutarPassoAsync(() => c.Ctx.PutCriteriosDesempateAsync(c.ObjetivaId), HttpStatusCode.NoContent, "PUT criterios-desempate", "/criterios-desempate");
        c.ResolucaoPesoAreaEnem = await PesosAreaEnemSeeder.SemearResolucaoAsync(c.Api);

        await ExecutarPassoAsync(
            () => c.Ctx.PutClassificacaoAsync(c.ObjetivaId, c.ResolucaoPesoAreaEnem), HttpStatusCode.NoContent, "PUT classificacao", "/classificacao");
        await ExecutarPassoAsync(
            () => c.Ctx.PutOpcoesDeclaradasAsync("EDICAO_ENEM", [new { codigo = "2026", rotulo = "2026" }]),
            HttpStatusCode.NoContent, "PUT fatos/EDICAO_ENEM/opcoes", "/fatos/EDICAO_ENEM/opcoes");
    }

    // Passo 9 — formulário de inscrição, respondido na fase que coleta a inscrição, e os itens dele
    // (COR_RACA de seleção única; BAIXA_RENDA exibido quando COR_RACA=PRETA).
    private static async Task DeclararFormularioAsync(Cenario c)
    {
        Guid faseDeInscricaoId = (await c.Ctx.ObterProcessoAsync()).CronogramaFases.Single(f => f.ColetaInscricao).Id;

        await ExecutarPassoAsync(
            () => c.Ctx.PutFormularioAsync("Formulário de Inscrição — PS Rico E2E", faseDeInscricaoId, ifMatch: null),
            HttpStatusCode.NoContent, "PUT formulario", "admin/processos-seletivos/{id}/formularios/INSCRICAO");
        await ExecutarPassoAsync(
            () => c.Ctx.PutFatosColetadosAsync(),
            HttpStatusCode.NoContent, "PUT itens do formulario", "admin/processos-seletivos/{id}/formularios/INSCRICAO/itens");
    }

    // Passos 10 a 13 — derivação (egresso de escola pública quando COR_RACA=PRETA), cascata de
    // remanejamento (matriz 8×7, fallback AC: INV-CASCATA exige cobrir as 8 modalidades da distribuição),
    // documentos exigidos (vazio satisfaz vacuamente a base legal das exigências) e divulgação.
    private static async Task DeclararRegrasEDocumentosAsync(Cenario c)
    {
        await ExecutarPassoAsync(() => c.Ctx.PutRegrasDerivacaoAsync(), HttpStatusCode.NoContent, "PUT regras-derivacao", "/regras-derivacao");
        await ExecutarPassoAsync(() => c.Ctx.PutCascataRemanejamentoAsync(), HttpStatusCode.NoContent, "PUT cascata-remanejamento", "/cascata-remanejamento");
        await ExecutarPassoAsync(() => c.Ctx.PutDocumentosExigidosAsync([]), HttpStatusCode.NoContent, "PUT documentos-exigidos", "/documentos-exigidos");
        await ExecutarPassoAsync(
            () => c.Ctx.PutDivulgacaoAsync(["numero_inscricao", "nome_abreviado"], justificativa: null, ifMatch: null),
            HttpStatusCode.NoContent, "PUT divulgacao", "/divulgacao");
    }

    // Passo 14 — GET /conformidade: projeção HTTP do checklist, não gate independente. A publicação
    // do passo 15 só devolve 204 porque todos os gates já estão satisfeitos, então o corpus rico
    // continua com todos os itens ok: a ampliação do checklist não pode criar falso vermelho aqui.
    private static async Task ConferirChecklistVerdeAsync(Cenario c)
    {
        HttpResponseMessage resposta = await ExecutarPassoAsync(() => c.Ctx.ObterConformidadeAsync(), HttpStatusCode.OK, "GET conformidade", "/conformidade");
        ConformidadeProcessoSeletivoDto conformidade = (await resposta.Content.ReadFromJsonAsync<ConformidadeProcessoSeletivoDto>())!;

        conformidade.ProcessoSeletivoId.Should().Be(
            c.Ctx.ProcessoId, "a coleção compartilha o Postgres com outros processos conformes — sem amarrar o ID, uma regressão no handler que ignorasse a rota devolveria a conformidade de outro processo");
        conformidade.Itens.Should().NotBeEmpty("uma lista vazia daria falso verde por AllSatisfy trivial");
        conformidade.Itens.Select(i => i.Codigo).Should().OnlyHaveUniqueItems("cada item do checklist aparece uma única vez");
        conformidade.Itens.Should().OnlyContain(i => i.Ok, "o processo está completo — nenhum item deveria estar pendente");
        conformidade.Itens.Select(i => i.Codigo).Should().BeEquivalentTo(
            ItensEsperadosDoChecklist,
            "o conjunto exato de itens que este corpus produz — remover um item do checklist, ou acrescentar um item indevido marcado ok, muda este conjunto");
    }

    // Passo 15 — publicar. A publicação em si é 204; o ato ainda NÃO existe em Publicações neste
    // instante (fila durável) — é o que o passo seguinte confirma.
    private static async Task PublicarV1Async(Cenario c)
    {
        Guid documentoV1 = await SemearDocumentoConfirmadoAsync(c.Api, c.Ctx.ProcessoId);

        await ExecutarPassoAsync(
            () => c.Ctx.PublicarAsync(documentoV1, numero: "042/2026", tipoAtoCodigo: "EDITAL_ABERTURA"),
            HttpStatusCode.NoContent, "POST publicacao", "/publicacao");
        c.AtoV1Id = await ObterAtoCriadorUnicoAsync(c.Api, c.Ctx.ProcessoId);
    }

    // Passo 16 — BARREIRA CAUSAL: confirmar o ato V1 registrado em Publicações antes de abrir a
    // sessão — "publicou" e "o ato existe" são afirmações diferentes. A identidade e o hash de V1,
    // gravados agora, são reconferidos no fechamento. O vínculo cross-módulo é provado pela
    // IDENTIDADE da versão que o ato guarda por valor, não pela existência dela.
    private static async Task ConfirmarAtoV1Async(Cenario c)
    {
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(c.Api, c.AtoV1Id);
        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");

        (string hashV1, Guid versaoV1Id) = await LerIdentidadeDaVersaoAsync(c.Api, c.AtoV1Id);
        c.HashV1 = hashV1;
        ato.VersaoDoObjeto.Should().NotBeNull("o ato guarda por valor a versão que ele governa (ADR-0075/0061)");
        ato.VersaoDoObjeto!.Id.Should().Be(versaoV1Id, "o ato de abertura governa exatamente V1");
        ato.VersaoDoObjeto.Hash.Should().Be(hashV1, "o hash guardado em Publicações é o mesmo que Seleção congelou para V1");
    }

    // Passo 17 — abrir a SESSÃO editorial (não o atalho atômico) e capturar o ETag da abertura.
    private static async Task AbrirSessaoEditorialAsync(Cenario c)
    {
        HttpResponseMessage abertura = await ExecutarPassoAsync(
            () => c.Ctx.AbrirRetificacaoAsync("Correção do peso da prova objetiva"),
            HttpStatusCode.Created, "POST retificacao-em-curso (abrir)", "/retificacao-em-curso");
        c.EtagDaAbertura = LerETag(abertura, "abrir sessão");
    }

    // Passo 18 — PUT de uma dimensão SOB a sessão, com o If-Match da abertura, capturando o ETag
    // NOVO. Chave de idempotência nova por requisição: nunca reutilizar.
    private static async Task AlterarEtapasSobASessaoAsync(Cenario c)
    {
        object[] etapasAlteradas = EtapasComPesoAlterado(c.ObjetivaId, c.RedacaoId, c.EntrevistaId);
        HttpResponseMessage mutacao = await ExecutarPassoAsync(
            () => c.Ctx.PutEtapasAsync(etapasAlteradas, c.EtagDaAbertura),
            HttpStatusCode.NoContent, "PUT etapas sob sessão", "/etapas");

        c.EtagNovo = LerETag(mutacao, "PUT etapas sob sessão");
        c.EtagNovo.Should().NotBe(c.EtagDaAbertura, "toda mutação aceita sob a sessão incrementa a revisão, e o controller devolve o tag novo");
    }

    // Passo 19 — fechar a sessão com o ETag NOVO (não o da abertura: o tag velho devolveria 412).
    private static async Task FecharSessaoEditorialAsync(Cenario c)
    {
        Guid documentoV2 = await SemearDocumentoConfirmadoAsync(c.Api, c.Ctx.ProcessoId);

        await ExecutarPassoAsync(
            () => c.Ctx.FecharRetificacaoAsync(documentoV2, numero: "042/2026-R", tipoAtoCodigo: "EDITAL_RETIFICACAO", c.EtagNovo),
            HttpStatusCode.NoContent, "POST retificacao-em-curso/fechamento", "/retificacao-em-curso/fechamento");
    }

    // Passo 20 — identidade das versões pelo AtoCriadorId: V2 retifica V1, nunca "qualquer ato do
    // processo". V1 permanece intacta (append-only).
    private static async Task ConferirIdentidadeDasVersoesAsync(Cenario c)
    {
        (Guid atoV2Id, Guid atoV2RetificaId, string hashV1AposFechamento, Guid versaoV2Id, string hashV2) =
            await LerVersoesAposFechamentoAsync(c.Api, c.Ctx.ProcessoId);

        atoV2Id.Should().NotBe(c.AtoV1Id, "o fechamento cria um ato novo — nunca reaproveita o de abertura");
        atoV2RetificaId.Should().Be(c.AtoV1Id, "V2.AtoCriadorRetificaId aponta para o ato que ela retifica: V1");
        hashV1AposFechamento.Should().Be(c.HashV1, "a versão anterior é imutável — o fechamento não muta o passado (append-only)");
        c.AtoV2Id = atoV2Id;
        c.VersaoV2Id = versaoV2Id;
        c.HashV2 = hashV2;
    }

    // Passo 21 — confirmar o ato V2 em Publicações pela identidade EXATA de V2, não "qualquer ato do
    // processo": é a barreira que o plano nomeia como bloqueante.
    private static async Task ConfirmarAtoV2Async(Cenario c)
    {
        AtoNormativo ato = await AtoNoRegistroCentral.LerAsync(c.Api, c.AtoV2Id);

        ato.AtoRetificadoId.Should().Be(c.AtoV1Id, "no ato registrado em Publicações, a relação de retificação espelha a de Seleção");
        ato.TipoCodigo.Should().Be("EDITAL_RETIFICACAO");
        ato.VersaoDoObjeto.Should().NotBeNull("o ato guarda por valor a versão que ele governa (ADR-0075/0061)");
        ato.VersaoDoObjeto!.Id.Should().Be(c.VersaoV2Id, "o ato retificador governa exatamente V2 — nunca V1 nem uma versão obsoleta");
        ato.VersaoDoObjeto.Hash.Should().Be(c.HashV2, "o hash guardado em Publicações é o mesmo que Seleção congelou para V2");
        c.HashV2.Should().NotBe(c.HashV1, "V2 congela outra configuração — se os hashes coincidissem, a asserção de identidade acima seria vácua");
    }

    // Passo 22 — GET /snapshot-vigente == versão 2. A diferença de hash sozinha não prova que a edição
    // entrou: toda retificação acrescenta o bloco `retificacao` ao envelope. A prova é o valor do peso
    // dentro da configuração congelada, casado pelo id da etapa — nunca por posição no array.
    private static async Task ConferirSnapshotVigenteAsync(Cenario c)
    {
        HttpResponseMessage resposta = await ExecutarPassoAsync(() => c.Ctx.ObterSnapshotVigenteAsync(), HttpStatusCode.OK, "GET snapshot-vigente", "/snapshot-vigente");
        SnapshotVigenteDto snapshot = (await resposta.Content.ReadFromJsonAsync<SnapshotVigenteDto>())!;

        snapshot.AtoId.Should().Be(c.AtoV2Id, "o snapshot vigente aponta para o ato que o governa — V2, depois do fechamento");
        LocalizarEtapaPorId(snapshot.Configuracao, c.ObjetivaId)["peso"]!.GetValue<string>().Should().Be(
            "4.0000", "V2 congela a configuração EDITADA sob a sessão — o peso da Prova Objetiva enviado no passo 18");
        snapshot.HashConfiguracao.Should().NotBe(c.HashV1, "V2 é uma versão congelada distinta de V1 — consequência de qualquer retificação, provada aqui só como reforço da asserção de peso acima");
        ConferirQuadroDePesosPorArea(snapshot.Configuracao["classificacao"]!, c.ResolucaoPesoAreaEnem);
        await ConferirQueSoHaDuasVersoesAsync(c);
    }

    // O quadro de pesos por área chega ao envelope publicado: a resolução declarada e um item por
    // grupo, ordenado pelo código do grupo.
    private static void ConferirQuadroDePesosPorArea(JsonNode classificacao, string resolucaoPesoAreaEnem)
    {
        classificacao["resolucaoPesoAreaEnem"]!.GetValue<string>().Should().Be(resolucaoPesoAreaEnem);
        classificacao["escalaNotaFinal"]!.GetValue<string>().Should().Be("1000.0000");
        classificacao["quadroPesoAreaEnem"]!.AsArray()
            .Select(static grupo => grupo!["grupoAreaEnem"]!["codigo"]!.GetValue<string>())
            .Should().Equal("HUMANISTICA_I", "HUMANISTICA_II", "SAUDE_E_BIOLOGICAS", "TECNOLOGICA");
    }

    private static async Task ConferirQueSoHaDuasVersoesAsync(Cenario c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        int total = await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == c.Ctx.ProcessoId);

        total.Should().Be(2, "publicação (V1) + fechamento da sessão (V2) — nada mais congelou uma versão");
    }
}
