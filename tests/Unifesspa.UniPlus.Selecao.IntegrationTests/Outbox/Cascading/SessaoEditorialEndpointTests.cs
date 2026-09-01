namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Application.DTOs;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A <b>sessão editorial</b> de retificação, fim a fim pelo HTTP (Story #860, ADR-0110).
/// </summary>
/// <remarks>
/// É aqui que os critérios que dependem do <b>ciclo real</b> se provam — e nenhum deles é
/// demonstrável em teste de unidade: o índice único que decide a corrida entre duas
/// aberturas (CA-02), o 412/428 que o mapeamento de erro precisa aflorar (CA-03), a
/// idempotência que <b>não</b> pode cachear a precondição (CA-05), o replay que precisa
/// devolver o ETag (CA-06) e a ausência de qualquer escrita congelada (CA-08).
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class SessaoEditorialEndpointTests
{
    private readonly CascadingFixture _fixture;

    public SessaoEditorialEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-02 — a unicidade é do ÍNDICE, não de um `if`
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-02: abrir a retificação duas vezes devolve 409 — a segunda cai no índice único")]
    public async Task Abrir_DuasVezes_Conflito()
    {
        Contexto ctx = await PublicarAsync(nameof(Abrir_DuasVezes_Conflito));

        HttpResponseMessage primeira = await ctx.AbrirAsync("Correção do prazo");
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segunda = await ctx.AbrirAsync("Outra correção");

        segunda.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "só há uma sessão editorial por certame — e quem garante isso é ux_rascunhos_retificacao_processo, não a checagem em memória, que duas aberturas concorrentes atravessariam juntas");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        int rascunhos = await db.RascunhosRetificacao.AsNoTracking()
            .CountAsync(r => r.ProcessoSeletivoId == ctx.ProcessoId);
        rascunhos.Should().Be(1);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-03 — a precondição protege os seis Definir*, não só o motivo
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-03: com sessão aberta, PUT /etapas SEM If-Match devolve 428; com If-Match DEFASADO devolve 412")]
    public async Task Definir_ComSessao_Precondicao()
    {
        Contexto ctx = await PublicarAsync(nameof(Definir_ComSessao_Precondicao));

        HttpResponseMessage abertura = await ctx.AbrirAsync("Correção do prazo");
        abertura.StatusCode.Should().Be(HttpStatusCode.Created);
        string etagInicial = LerETag(abertura);

        // Sem precondição: o servidor sabe que há sessão, e o cliente não disse qual estado
        // está editando.
        HttpResponseMessage semIfMatch = await ctx.PutEtapasAsync(ifMatch: null);
        semIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        // Com a precondição certa: aceita, e devolve o ETag NOVO — a revisão avançou.
        HttpResponseMessage aceito = await ctx.PutEtapasAsync(ifMatch: etagInicial);
        aceito.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string etagNovo = LerETag(aceito);
        etagNovo.Should().NotBe(etagInicial, "toda mutação aceita incrementa a revisão");

        // O tag antigo agora está defasado — é a edição cega de um segundo administrador.
        HttpResponseMessage defasado = await ctx.PutEtapasAsync(ifMatch: etagInicial);
        defasado.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact(DisplayName = "Sem sessão aberta, PUT /etapas num processo publicado continua 422 — a precondição não é exigida onde não há o que proteger")]
    public async Task Definir_PublicadoSemSessao_Recusa()
    {
        Contexto ctx = await PublicarAsync(nameof(Definir_PublicadoSemSessao_Recusa));

        HttpResponseMessage resposta = await ctx.PutEtapasAsync(ifMatch: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-15 (issue #563) — o endpoint de divulgação pública participa do MESMO
    // contrato de sessão editorial que /etapas já prova acima; aqui só se prova que a
    // ROTA NOVA está corretamente ligada a ele (428/412/ETag e o valor persistido).
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-15: PUT /divulgacao SEM If-Match devolve 428; com If-Match DEFASADO devolve 412; aceito devolve ETag novo e persiste")]
    public async Task Definir_Divulgacao_ComSessao_Precondicao()
    {
        Contexto ctx = await PublicarAsync(nameof(Definir_Divulgacao_ComSessao_Precondicao));

        HttpResponseMessage abertura = await ctx.AbrirAsync("Ampliar a divulgação pública");
        string etagInicial = LerETag(abertura);

        HttpResponseMessage semIfMatch = await ctx.PutDivulgacaoAsync(["numero_inscricao", "nome_abreviado"], null, ifMatch: null);
        semIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        HttpResponseMessage aceito = await ctx.PutDivulgacaoAsync(["numero_inscricao", "nome_abreviado"], null, ifMatch: etagInicial);
        aceito.StatusCode.Should().Be(HttpStatusCode.NoContent);
        string etagNovo = LerETag(aceito);
        etagNovo.Should().NotBe(etagInicial, "toda mutação aceita incrementa a revisão da sessão");

        HttpResponseMessage defasado = await ctx.PutDivulgacaoAsync(["numero_inscricao"], null, ifMatch: etagInicial);
        defasado.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        ProcessoSeletivoDto processo = await ctx.ObterProcessoAsync();
        processo.ConfiguracaoDivulgacao.Should().NotBeNull(
            "a mutação aceita persistiu — o read-back administrativo tem de refletir o que foi salvo");
        processo.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal("nome_abreviado", "numero_inscricao");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-05 — a precondição NÃO envenena a idempotência
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-05: um 412 NÃO é cacheado — o cliente relê o ETag, retenta com a MESMA Idempotency-Key, e a operação EXECUTA")]
    public async Task Precondicao_Falhou_NaoEArmazenada()
    {
        Contexto ctx = await PublicarAsync(nameof(Precondicao_Falhou_NaoEArmazenada));

        HttpResponseMessage abertura = await ctx.AbrirAsync("Correção do prazo");
        string etagInicial = LerETag(abertura);

        // O cliente queima a revisão numa primeira edição...
        await ctx.PutEtapasAsync(ifMatch: etagInicial);

        // ...e agora tenta outra com o tag velho, sob uma key nova. Leva 412.
        string chave = MakeIdempotencyKey();
        HttpResponseMessage falhou = await ctx.PutEtapasAsync(ifMatch: etagInicial, idempotencyKey: chave);
        falhou.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        // Relê o ETag e retenta — com a MESMA key e o MESMO body.
        string etagAtual = LerETag(await ctx.ObterSessaoAsync());
        HttpResponseMessage retentado = await ctx.PutEtapasAsync(ifMatch: etagAtual, idempotencyKey: chave);

        retentado.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "o 412 não é resultado da operação — a operação NÃO EXECUTOU. Se ele fosse gravado sob a key, o cliente que corrigiu o If-Match receberia o mesmo 412 em replay por 24h, e a sessão editorial ficaria travada (ADR-0110 D6, exceção formal à ADR-0027)");
        retentado.Headers.Contains("Idempotency-Replayed").Should().BeFalse("a chamada corrigida executou de verdade, não veio do cache");
    }

    [Fact(DisplayName = "Um 428 também não é armazenado — a resposta depende de um header AUSENTE (RFC 6585 §3)")]
    public async Task Precondicao_Requerida_NaoEArmazenada()
    {
        Contexto ctx = await PublicarAsync(nameof(Precondicao_Requerida_NaoEArmazenada));

        HttpResponseMessage abertura = await ctx.AbrirAsync("Correção do prazo");
        string etag = LerETag(abertura);

        string chave = MakeIdempotencyKey();
        HttpResponseMessage semIfMatch = await ctx.PutEtapasAsync(ifMatch: null, idempotencyKey: chave);
        semIfMatch.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        HttpResponseMessage comIfMatch = await ctx.PutEtapasAsync(ifMatch: etag, idempotencyKey: chave);
        comIfMatch.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-06 — o replay devolve o ETag
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-06: o replay de uma abertura devolve o ETag gravado — sem ele o cliente ficaria sem a precondição da próxima chamada")]
    public async Task Abrir_Replay_DevolveETag()
    {
        Contexto ctx = await PublicarAsync(nameof(Abrir_Replay_DevolveETag));

        string chave = MakeIdempotencyKey();
        HttpResponseMessage primeira = await ctx.AbrirAsync("Correção do prazo", chave);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string etag = LerETag(primeira);

        // Mesma key, mesmo body — o cliente retentou por timeout de rede.
        HttpResponseMessage replay = await ctx.AbrirAsync("Correção do prazo", chave);

        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        LerETag(replay).Should().Be(
            etag,
            "o filtro guardava só Content-Type e Location, e excluía o ETag por escrito — um replay sem ele deixaria o cliente sem como mutar, num caminho em que a primeira execução lhe deu o tag de graça");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-08 — abrir e editar não congelam NADA
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-08: abrir e alterar produzem ZERO nova VersaoConfiguracao e ZERO ProcessoPublicadoEvent")]
    public async Task SessaoEditorial_NaoCongelaNada()
    {
        Contexto ctx = await PublicarAsync(nameof(SessaoEditorial_NaoCongelaNada));

        DomainEventCollector collector = ctx.Api.Services.GetRequiredService<DomainEventCollector>();
        int versoesAntes = await ContarVersoesAsync(ctx);

        // A publicação que monta o cenário entrega o evento pela fila durável. Só limpar o
        // coletor depois de observar esse evento evita atribuí-lo às edições abaixo.
        await AguardarEventoDaPublicacaoAsync(collector, ctx.ProcessoId);
        collector.Clear();

        HttpResponseMessage abertura = await ctx.AbrirAsync("Correção do prazo");
        string etag = LerETag(abertura);
        (await ctx.PutEtapasAsync(ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        string etagNovo = LerETag(await ctx.ObterSessaoAsync());
        HttpResponseMessage motivo = await ctx.AlterarMotivoAsync("Motivo revisado", etagNovo);
        motivo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ContarVersoesAsync(ctx)).Should().Be(
            versoesAntes,
            "a versão nova nasce só no FECHAMENTO — enquanto a sessão está aberta, o que vale para o candidato continua sendo a versão congelada vigente");

        collector.Snapshot().Should().NotContain(
            e => e.ProcessoSeletivoId == ctx.ProcessoId,
            "nenhum ato é emitido: abrir e editar não são fatos publicáveis");
    }

    [Fact(DisplayName = "CA-10: o status do processo NÃO muda ao abrir a retificação — ele continua Publicado")]
    public async Task Abrir_NaoMudaStatus()
    {
        Contexto ctx = await PublicarAsync(nameof(Abrir_NaoMudaStatus));

        (await ctx.AbrirAsync("Correção do prazo")).StatusCode.Should().Be(HttpStatusCode.Created);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking()
            .SingleAsync(p => p.Id == ctx.ProcessoId);

        processo.Status.Should().Be(
            Domain.Enums.StatusProcesso.Publicado,
            "o status marca o estado do ATO — um certame com retificação aberta está publicado, e o DTO público não pode dizer o contrário (D3)");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // D7 — o atalho atômico recusa com sessão aberta, e SÓ nessa direção: uma cadeia
    // recém-sucedida pelo atalho não impede a abertura, porque o certame continua
    // publicado e a sessão nasce sobre a versão nova. Os três testes abaixo são as
    // duas ordens determinísticas mais a corrida que prova a serialização entre elas.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "D7: com sessão aberta, o atalho POST /retificacoes devolve 409")]
    public async Task Atalho_ComSessaoAberta_Conflito()
    {
        Contexto ctx = await PublicarAsync(nameof(Atalho_ComSessaoAberta_Conflito));
        (await ctx.AbrirAsync("Sessão em curso")).StatusCode.Should().Be(HttpStatusCode.Created);

        Guid documento = await SemearDocumentoConfirmadoAsync(ctx.Api, ctx.ProcessoId);
        HttpResponseMessage atalho = await ctx.PostAtalhoRetificacaoAsync(documento, "Atalho concorrente");

        atalho.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "D7: com a cadeia recém-sucedida pelo atalho, abrir a sessão é ACEITO — e o rascunho nasce sobre a versão nova")]
    public async Task Atalho_DepoisAbrir_SessaoNasceSobreAVersaoNova()
    {
        Contexto ctx = await PublicarAsync(nameof(Atalho_DepoisAbrir_SessaoNasceSobreAVersaoNova));
        Guid documento = await SemearDocumentoConfirmadoAsync(ctx.Api, ctx.ProcessoId);

        (await ctx.PostAtalhoRetificacaoAsync(documento, "Atalho atômico")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage abertura = await ctx.AbrirAsync("Sessão editorial depois do atalho");

        abertura.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "a recusa entre os dois caminhos é de MÃO ÚNICA: uma sessão aberta bloqueia o atalho, porque o atalho "
            + "congelaria a configuração que a sessão está no meio de editar. O inverso não bloqueia nada — um "
            + "certame continua publicado depois de retificado, e retificá-lo de novo, agora pela sessão editorial, "
            + "é o mesmo que abrir a primeira");

        CadeiaDeConfiguracao cadeia = await LerCadeiaAsync(ctx);
        cadeia.Rascunhos.Should().Be(1);
        cadeia.VersaoBaseIdDoRascunho.Should().Be(
            cadeia.IdDoTopo,
            "a sessão se ancora na versão que o atalho acabou de criar — se ela nascesse presa à anterior, o "
            + "fechamento emendaria um ato já emendado e seria recusado por BaseDesatualizada");
    }

    /// <summary>
    /// Nada obriga as duas requisições a se sobreporem: elas são só disparadas juntas, e numa
    /// rodada isolada podem acabar se serializando sozinhas — desfecho indistinguível do que o
    /// lock produz, e que passaria mesmo sem ele. Daí a repetição. Medida com o
    /// <c>FOR UPDATE</c> removido, a sobreposição acontece em 29 de 30 rodadas; cinco
    /// tentativas põem a chance de a perda do lock escapar abaixo de uma em dez milhões, sem
    /// custo perceptível e — o que importa mais — <b>sem ponto de sincronização no código de
    /// produção</b>, que é o preço que uma barreira determinística cobraria.
    /// </summary>
    private const int RodadasDaCorrida = 5;

    [Fact(DisplayName = "D7 sob CORRIDA: abrir a sessão e usar o atalho ao mesmo tempo — o rascunho nasce sempre sobre o TOPO da cadeia, seja qual for a ordem")]
    public async Task Abrir_ConcorrenteComAtalho_RascunhoNasceSobreOTopo()
    {
        for (int rodada = 1; rodada <= RodadasDaCorrida; rodada++)
        {
            await CorridaEntreAberturaEAtalhoAsync(rodada);
        }
    }

    private async Task CorridaEntreAberturaEAtalhoAsync(int rodada)
    {
        Contexto ctx = await PublicarAsync($"{nameof(Abrir_ConcorrenteComAtalho_RascunhoNasceSobreOTopo)} rodada {rodada}");
        Guid documento = await SemearDocumentoConfirmadoAsync(ctx.Api, ctx.ProcessoId);
        int versoesAntes = await ContarVersoesAsync(ctx);

        // Quem serializa os dois é o FOR UPDATE da raiz, que ambos tomam em
        // ObterParaMutacaoAsync; o segundo a chegar só prossegue depois do commit do primeiro,
        // e enxerga o estado que ele deixou. A ordem em que eles alcançam o lock é do
        // escalonador — e ela decide o desfecho, sem que nenhum dos dois seja irregular.
        Task<HttpResponseMessage> abrir = ctx.AbrirAsync("Sessão editorial");
        Task<HttpResponseMessage> atalho = ctx.PostAtalhoRetificacaoAsync(documento, "Atalho atômico");

        await Task.WhenAll(abrir, atalho);
        HttpResponseMessage resultadoAbrir = await abrir;
        HttpResponseMessage resultadoAtalho = await atalho;

        // Um 5xx aqui é BUG, e ele precisa se anunciar como tal — um deadlock ou um lock
        // timeout viraria, sem esta guarda, uma falha adiante falando de outra coisa.
        ((int)resultadoAbrir.StatusCode).Should().BeLessThan(
            500, $"a abertura não pode falhar com erro de servidor numa corrida — veio {resultadoAbrir.StatusCode}");
        ((int)resultadoAtalho.StatusCode).Should().BeLessThan(
            500, $"o atalho não pode falhar com erro de servidor numa corrida — veio {resultadoAtalho.StatusCode}");

        // A abertura não tem por que ser recusada em nenhuma das duas ordens: ou ela chega
        // primeiro ao lock, ou chega depois e abre sobre o topo que o atalho acabou de criar.
        resultadoAbrir.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"nada no domínio recusa a abertura de uma sessão sobre um certame publicado — rodada {rodada}, atalho={resultadoAtalho.StatusCode}");

        resultadoAtalho.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NoContent, HttpStatusCode.Conflict],
            "o atalho ou tomou o lock primeiro e sucedeu a cadeia, ou o tomou depois, encontrou o rascunho e recusou");

        CadeiaDeConfiguracao cadeia = await LerCadeiaAsync(ctx);

        cadeia.Versoes.Should().Be(
            resultadoAtalho.StatusCode == HttpStatusCode.NoContent ? versoesAntes + 1 : versoesAntes,
            "a versão nova nasce do atalho, e só dele — abrir não congela nada, e o fechamento nem chegou a ser pedido");

        cadeia.Rascunhos.Should().Be(1, "a abertura foi aceita, e só há uma sessão editorial por certame");

        // ESTA é a asserção que morre se a serialização sumir. Sem o lock, a abertura leria a
        // versão N enquanto o atalho grava a N+1, e o rascunho ficaria ancorado numa base que
        // deixou de ser o topo — a sessão nasceria condenada, e o administrador só descobriria
        // ao tentar fechá-la (BaseDesatualizada).
        cadeia.VersaoBaseIdDoRascunho.Should().Be(
            cadeia.IdDoTopo,
            $"o rascunho tem de estar ancorado no topo da cadeia — rodada {rodada}, "
            + $"atalho={resultadoAtalho.StatusCode}, versões {versoesAntes}->{cadeia.Versoes}");
        cadeia.NumeroVersaoBaseDoRascunho.Should().Be(cadeia.NumeroDoTopo);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // GET
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "GET da sessão devolve 404 quando não há retificação em curso")]
    public async Task Obter_SemSessao_NaoEncontrado()
    {
        Contexto ctx = await PublicarAsync(nameof(Obter_SemSessao_NaoEncontrado));

        HttpResponseMessage resposta = await ctx.ObterSessaoAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PUT do motivo sem If-Match devolve 428 mesmo sem sessão aberta — falha de protocolo precede a checagem do recurso")]
    public async Task AlterarMotivo_SemIfMatch_SemSessao_428()
    {
        Contexto ctx = await PublicarAsync(nameof(AlterarMotivo_SemIfMatch_SemSessao_428));

        HttpResponseMessage resposta = await ctx.AlterarMotivoAsync("Motivo", ifMatch: null);

        resposta.StatusCode.Should().Be(
            HttpStatusCode.PreconditionRequired,
            "esta rota só existe PARA a sessão: o If-Match é incondicional aqui, e a sua ausência é respondida antes do 409 de rascunho inexistente (D9, '3 antes de 10')");
    }

    [Fact(DisplayName = "If-Match sintaticamente inválido devolve 400 — não 412")]
    public async Task IfMatchMalformado_400()
    {
        Contexto ctx = await PublicarAsync(nameof(IfMatchMalformado_400));
        (await ctx.AbrirAsync("Correção")).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage resposta = await ctx.PutEtapasAsync(ifMatch: "sem-aspas");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Um 400 de If-Match malformado NÃO é armazenado — o cliente corrige SÓ O HEADER e a mesma key executa")]
    public async Task IfMatchMalformado_NaoEArmazenado()
    {
        Contexto ctx = await PublicarAsync(nameof(IfMatchMalformado_NaoEArmazenado));

        HttpResponseMessage abertura = await ctx.AbrirAsync("Correção do prazo");
        string etag = LerETag(abertura);

        // O defeito está no HEADER — e a idempotência identifica a requisição pelo hash do
        // BODY, que aqui não muda. Se este 400 fosse gravado, o cliente que corrigisse o
        // If-Match e retentasse com a mesma key receberia o mesmo 400 em replay por 24h,
        // sem nada que ele pudesse fazer a respeito: o body dele já estava certo.
        string chave = MakeIdempotencyKey();
        HttpResponseMessage malformado = await ctx.PutEtapasAsync(ifMatch: "sem-aspas", idempotencyKey: chave);
        malformado.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage corrigido = await ctx.PutEtapasAsync(ifMatch: etag, idempotencyKey: chave);

        corrigido.StatusCode.Should().Be(HttpStatusCode.NoContent);
        corrigido.Headers.Contains("Idempotency-Replayed").Should().BeFalse();
    }

    [Fact(DisplayName = "Um 400 cujo defeito está no BODY continua cacheado — ali o hash do corpo já distingue a correção")]
    public async Task BadRequestSemIfMatch_ContinuaArmazenado()
    {
        Contexto ctx = await PublicarAsync(nameof(BadRequestSemIfMatch_ContinuaArmazenado));

        // Idempotency-Key malformada: 400 de transporte, sem If-Match na requisição. Este
        // caminho NÃO pode ter mudado — a liberação da reserva vale só para o 400 de
        // precondição, e o resto da ADR-0027 continua valendo.
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{ctx.ProcessoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(Array.Empty<object>()),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "chave inválida com espaços");

        HttpResponseMessage resposta = await ctx.Client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Infraestrutura do cenário
    // ══════════════════════════════════════════════════════════════════════════════

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> AbrirAsync(string motivo, string? idempotencyKey = null)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacao-em-curso", UriKind.Relative))
            {
                Content = JsonContent.Create(new { motivo }),
            };
            AppendTestAuth(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> ObterSessaoAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacao-em-curso", UriKind.Relative));
            AppendTestAuth(request);
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> AlterarMotivoAsync(string motivo, string? ifMatch)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacao-em-curso", UriKind.Relative))
            {
                Content = JsonContent.Create(new { motivo }),
            };
            AppendTestAuth(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// O <c>PUT /etapas</c> é o representante dos <b>seis</b> <c>Definir*</c>: eles
        /// compartilham o guard, e é o guard que estes testes provam. Body constante — a
        /// idempotência identifica a requisição pelo hash dele, e um payload que variasse
        /// entre a chamada que leva 412 e a que a corrige daria <b>body mismatch</b> (422)
        /// em vez de executar, e o CA-05 provaria outra coisa.
        /// </summary>
        public async Task<HttpResponseMessage> PutEtapasAsync(string? ifMatch, string? idempotencyKey = null)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/etapas", UriKind.Relative))
            {
                Content = JsonContent.Create(new[]
                {
                    // Classificatória (1) com peso: ao menos uma etapa precisa COMPOR a nota,
                    // senão o divisor da média sai zero e o domínio recusa (422) antes mesmo
                    // de a precondição virar o assunto.
                    new
                    {
                        nome = "Prova Objetiva", carater = 1,
                        tipoEtapaOrigemId = new Guid("019fee1e-7000-7000-8000-000000000001"),
                        peso = 1.0m, notaMinima = (decimal?)null, ordem = 1,
                    },
                }),
            };
            AppendTestAuth(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey ?? MakeIdempotencyKey());
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        /// <summary>PUT /divulgacao (issue #563) — CamposPublicos nulo remove a configuração explícita.</summary>
        public async Task<HttpResponseMessage> PutDivulgacaoAsync(IReadOnlyList<string>? camposPublicos, string? justificativa, string? ifMatch)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/divulgacao", UriKind.Relative))
            {
                Content = JsonContent.Create(new { camposPublicos, justificativa }),
            };
            AppendTestAuth(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            if (ifMatch is not null)
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<ProcessoSeletivoDto> ObterProcessoAsync()
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}", UriKind.Relative));
            AppendTestAuth(request);
            HttpResponseMessage resposta = await Client.SendAsync(request).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await resposta.Content.ReadFromJsonAsync<ProcessoSeletivoDto>(JsonDoContrato.Opcoes))!;
        }

        public async Task<HttpResponseMessage> PostAtalhoRetificacaoAsync(Guid documentoId, string motivo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/retificacoes", UriKind.Relative))
            {
                Content = JsonContent.Create(new
                {
                    motivo,
                    numero = "001/2026-R1",
                    documentoEditalId = documentoId,
                    ato = NovoAto("EDITAL_RETIFICACAO"),
                }),
            };
            AppendTestAuth(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }

    private async Task<Contexto> PublicarAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        Contexto ctx = new(api, client, processoId);

        using HttpRequestMessage publicar = new(
            HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2026",
                documentoEditalId = documentoId,
                ato = NovoAto(),
            }),
        };
        AppendTestAuth(publicar);
        publicar.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        HttpResponseMessage resposta = await client.SendAsync(publicar);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado");

        return ctx;
    }

    private static async Task<int> ContarVersoesAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .CountAsync(v => v.ProcessoSeletivoId == ctx.ProcessoId);
    }

    private static async Task AguardarEventoDaPublicacaoAsync(DomainEventCollector collector, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < limite)
        {
            if (collector.Snapshot().Any(e => e.ProcessoSeletivoId == processoId))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        throw new TimeoutException("A publicação que monta o cenário não entregou o evento esperado.");
    }

    /// <summary>
    /// O topo da cadeia de configuração e a base em que a sessão editorial se ancorou — o par
    /// que permite dizer se um rascunho está preso à versão corrente ou a uma que já foi
    /// sucedida. O topo é a de maior <c>NumeroVersao</c>, o mesmo critério que
    /// <c>ObterVersaoAtualAsync</c> usa.
    /// </summary>
    private sealed record CadeiaDeConfiguracao(
        int Versoes,
        int NumeroDoTopo,
        Guid IdDoTopo,
        int Rascunhos,
        int? NumeroVersaoBaseDoRascunho,
        Guid? VersaoBaseIdDoRascunho);

    private static async Task<CadeiaDeConfiguracao> LerCadeiaAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == ctx.ProcessoId)
            .OrderByDescending(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().NotBeEmpty("o cenário parte de um certame publicado, que já tem a versão de abertura");

        RascunhoRetificacao? rascunho = await db.RascunhosRetificacao.AsNoTracking()
            .SingleOrDefaultAsync(r => r.ProcessoSeletivoId == ctx.ProcessoId);

        return new CadeiaDeConfiguracao(
            versoes.Count,
            versoes[0].NumeroVersao,
            versoes[0].Id,
            rascunho is null ? 0 : 1,
            rascunho?.NumeroVersaoBase,
            rascunho?.VersaoBaseId);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hashFixo = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    /// <summary>
    /// O ETag é o contrato da sessão — se ele não vier no header, não há o que asserir, e a
    /// falha tem de dizer isso, e não estourar um <c>NullReferenceException</c> três linhas
    /// adiante.
    /// </summary>
    private static string LerETag(HttpResponseMessage resposta)
    {
        resposta.Headers.ETag.Should().NotBeNull(
            $"a resposta {(int)resposta.StatusCode} de uma sessão editorial carrega o ETag — é a precondição da chamada seguinte");
        return resposta.Headers.ETag!.ToString();
    }

    /// <summary>
    /// O tipo do ato vem DECLARADO pelo operador e é conferido contra o catálogo de
    /// Publicações (ADR-0103) — o código é o que o <c>TiposDeAtoSeeder</c> semeia, e a data
    /// de publicação tem de cair dentro da vigência dele.
    /// </summary>
    private static object NovoAto(string tipoAtoCodigo = "EDITAL_ABERTURA") => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataPublicacao = Hoje(),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo,
    };

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HojeMais(int dias) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
