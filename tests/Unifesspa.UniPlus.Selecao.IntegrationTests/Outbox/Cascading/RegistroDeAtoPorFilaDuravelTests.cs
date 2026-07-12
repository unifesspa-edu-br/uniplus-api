namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Prova, contra o host real (Wolverine + PG queue durável + Postgres via
/// Testcontainers), as quatro propriedades que decidem o desenho da orquestração
/// Seleção → Publicações. Nenhuma delas foi assumida: cada uma é medida.
/// </summary>
/// <remarks>
/// O que se está tentando derrubar: que a publicação e o registro do ato possam divergir
/// de um jeito que trave o certame. A v1 do plano (chamada síncrona, ato primeiro) tinha
/// esse defeito — um ato órfão reserva a vaga de linhagem do objeto (ADR-0107) e o
/// certame nunca mais publica. A v2 (transação compartilhada) é inviável no Wolverine.
/// A decisão (ADR-0108): a requisição do ato viaja no outbox, na mesma transação do Edital.
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class RegistroDeAtoPorFilaDuravelTests
{
    private static readonly TimeSpan Paciencia = TimeSpan.FromSeconds(30);

    private readonly CascadingFixture _fixture;

    public RegistroDeAtoPorFilaDuravelTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "publicar enfileira o ato no outbox e Publicações o registra depois do commit")]
    public async Task Publicar_RegistraAtoEmPublicacoes_PelaFilaDuravel()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Publicar_RegistraAtoEmPublicacoes_PelaFilaDuravel));

        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O Edital já existe assim que o 204 volta — a publicação NÃO espera Publicações.
        Guid editalId = await ObterEditalIdAsync(api, processoId);

        // O ato chega pela fila durável, logo depois do commit.
        AtoNormativo? ato = await AguardarAtoAsync(api, editalId);

        ato.Should().NotBeNull("o ato é registrado a partir da mensagem persistida no outbox");
        ato!.Id.Should().Be(editalId, "o id do ato é decidido por Seleção — é o que torna a reentrega idempotente");
        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        // O vínculo com o certame (ADR-0105) — sem ele a consulta "atos deste certame" não acha nada.
        (await db.Set<VinculoAtoEntidade>().AsNoTracking()
            .AnyAsync(v => v.AtoId == editalId && v.EntidadeTipo == "PROCESSO_SELETIVO" && v.EntidadeId == processoId))
            .Should().BeTrue();

        // A vaga de linhagem foi reservada — pela linhagem CERTA.
        (await db.Set<LinhagemUnicaPorObjeto>().AsNoTracking()
            .AnyAsync(l => l.EntidadeId == processoId && l.TipoCodigo == "EDITAL_ABERTURA" && l.RaizId == editalId))
            .Should().BeTrue();
    }

    [Fact(DisplayName = "o registro recusado NÃO reserva a vaga do certame (o defeito que condenou o desenho síncrono)")]
    public async Task RegistroRecusado_NaoPrendeAVagaDoCertame()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(RegistroRecusado_NaoPrendeAVagaDoCertame));
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid editalId = await ObterEditalIdAsync(api, processoId);
        editalId.Should().NotBeEmpty("a publicação de Seleção não espera Publicações aceitar o ato");
        (await AguardarAtoAsync(api, editalId)).Should().NotBeNull();

        // Agora uma requisição que Publicações RECUSA: tipo sem versão vigente. É o caso
        // real de falha — um código de tipo que o catálogo não conhece na data.
        Guid processoOutro = (await SemearProcessoAsync(api, $"{nameof(RegistroRecusado_NaoPrendeAVagaDoCertame)}-recusado")).ProcessoId;
        Guid atoRecusado = Guid.CreateVersion7();

        Func<Task> entrega = async () =>
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: atoRecusado,
                Orgao: "CEPS",
                Serie: "EDITAL",
                Ano: 2026,
                Numero: "999/2026",
                TipoCodigo: "TIPO_QUE_NAO_EXISTE_NO_CATALOGO",
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('a', 64),
                Assinante: "Spike",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoOutro)],
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(true, true, false)));
        };

        // A recusa ESCAPA como exceção — é isso que faz o Wolverine retentar e, esgotado,
        // mandar para a dead letter. Um Result.Failure aqui seria lido como sucesso e o ato
        // sumiria em silêncio.
        await entrega.Should().ThrowAsync<Exception>();

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        (await db.Set<AtoNormativo>().AsNoTracking().AnyAsync(a => a.Id == atoRecusado))
            .Should().BeFalse("o ato não foi registrado — o tipo não existe no catálogo");

        // ESTE é o ponto da story. A vaga do certame NÃO foi reservada por um ato que não
        // chegou a existir. No desenho síncrono (ato primeiro), o órfão teria tomado a vaga
        // — e a vaga é monotônica (ADR-0107): o certame ficaria impublicável para sempre.
        (await db.Set<LinhagemUnicaPorObjeto>().AsNoTracking().AnyAsync(l => l.EntidadeId == processoOutro))
            .Should().BeFalse("nenhuma vaga é reservada por um registro recusado");

        // E o que Seleção publicou permanece publicado — os dois módulos falham em separado.
        await using AsyncServiceScope scopeSelecao = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = scopeSelecao.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await selecao.Editais.AsNoTracking().AnyAsync(e => e.Id == editalId)).Should().BeTrue();
    }

    [Fact(DisplayName = "publicar e retificar em sequência registram OS DOIS atos, com a cadeia — a ordem da fila não decide")]
    public async Task PublicarERetificar_RegistramOsDoisAtos_EmCadeia()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoAbertura) = await SemearProcessoAsync(api, nameof(PublicarERetificar_RegistramOsDoisAtos_EmCadeia));

        // Publicar e retificar em sequência põem DUAS requisições na fila. A da retificação
        // emenda o ato que a primeira ainda vai criar — e nada garante a ordem em que a fila
        // as processa. Se a retificação chegar primeiro, o predecessor "não existe": é uma
        // recusa de ORDEM, que o retry resolve, e não de mérito, que iria para a dead letter.
        (await PublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        (await RetificarAsync(client, processoId, documentoRetificacao)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scopeSelecao = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = scopeSelecao.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Guid abertura = await selecao.Editais.AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId && e.Natureza == Domain.Enums.NaturezaEdital.Abertura)
            .Select(e => e.Id).SingleAsync();
        Guid retificacao = await selecao.Editais.AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId && e.Natureza == Domain.Enums.NaturezaEdital.Retificacao)
            .Select(e => e.Id).SingleAsync();

        AtoNormativo? atoAbertura = await AguardarAtoAsync(api, abertura);
        AtoNormativo? atoRetificacao = await AguardarAtoAsync(api, retificacao);

        atoAbertura.Should().NotBeNull();
        atoRetificacao.Should().NotBeNull("a retificação não pode ficar na dead letter porque chegou antes do ato que emenda");
        atoRetificacao!.AtoRetificadoId.Should().Be(abertura, "a cadeia em Publicações espelha a de Seleção");

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        // A retificação HERDA o vínculo com o certame — sem isso ela sumiria da consulta dos
        // atos do certame, que passaria a exibir só a versão superada.
        (await db.Set<VinculoAtoEntidade>().AsNoTracking()
            .AnyAsync(v => v.AtoId == retificacao && v.EntidadeId == processoId))
            .Should().BeTrue();

        // E a vaga do objeto continua sendo UMA — a da linhagem, cuja raiz é a abertura.
        (await db.Set<LinhagemUnicaPorObjeto>().AsNoTracking()
            .CountAsync(l => l.EntidadeId == processoId && l.TipoCodigo == "EDITAL_ABERTURA"))
            .Should().Be(1, "a retificação pertence à linhagem que já ocupa o objeto — não abre uma segunda vaga");
    }

    [Fact(DisplayName = "retificação enfileirada é validada como a do caminho HTTP — classe de congelamento divergente é recusada (ADR-0103)")]
    public async Task RetificacaoEnfileirada_ComClasseDeCongelamentoDivergente_EhRecusada()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeAtoAsync(api, "AVISO_NAO_CONGELANTE", unicoPorObjeto: false, congela: false);

        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(RetificacaoEnfileirada_ComClasseDeCongelamentoDivergente_EhRecusada));
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid editalId = await ObterEditalIdAsync(api, processoId);
        (await AguardarAtoAsync(api, editalId)).Should().NotBeNull();

        // Uma requisição que emenda o edital (congelante) com um ato NÃO congelante. A
        // invariante da ADR-0103 é congela(retificador) == congela(retificado): registrar
        // isto corromperia a linhagem entre o ato e a configuração que ele congela.
        //
        // O handler de mensagem não reimplementa essa checagem — ele delega ao mesmo
        // handler do caminho HTTP. Este teste é o que trava a delegação: se alguém voltar a
        // duplicar a lógica aqui, a recusa some e o teste cai.
        Guid atoInvalido = Guid.CreateVersion7();
        Func<Task> entrega = async () =>
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: atoInvalido,
                Orgao: "CEPS",
                Serie: "AVISO",
                Ano: 2026,
                Numero: "010/2026",
                TipoCodigo: "AVISO_NAO_CONGELANTE",
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('b', 64),
                Assinante: "Diretor do CEPS",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: editalId,
                MotivoRetificacao: "Tentativa de emendar um ato congelante com um não congelante",
                Vinculos: [],
                // Os atributos conferidos: este ato declara NÃO congelar, e emenda um que congela.
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(false, false, false)));
        };

        await entrega.Should().ThrowAsync<Exception>();

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        (await db.Set<AtoNormativo>().AsNoTracking().AnyAsync(a => a.Id == atoInvalido))
            .Should().BeFalse("a emenda entre classes de congelamento distintas é recusada, não persistida");
    }

    [Fact(DisplayName = "publicar declarando um tipo que NÃO congela configuração é recusado com 422 — antes de qualquer escrita")]
    public async Task Publicar_ComTipoQueNaoCongela_Recusa422_SemEscrever()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeAtoAsync(api, "AVISO_NAO_CONGELANTE", unicoPorObjeto: false, congela: false);

        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Publicar_ComTipoQueNaoCongela_Recusa422_SemEscrever));

        // Publicar CONGELA a configuração numa nova versão (RN08). Um ato que declara não
        // congelar não pode ser o criador dela — o ato diria uma coisa e a versão provaria
        // outra. E a incoerência não ficaria parada: a retificação seguinte, com um tipo
        // congelante, seria recusada por classe divergente e ficaria sem ato.
        HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId, "AVISO_NAO_CONGELANTE");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // E nada foi escrito: a conferência acontece ANTES de publicar. Sem ela, o Edital
        // sairia publicado com 204 e a recusa só apareceria na dead letter.
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await selecao.Editais.AsNoTracking().AnyAsync(e => e.ProcessoSeletivoId == processoId))
            .Should().BeFalse("a recusa precede qualquer escrita");
    }

    [Fact(DisplayName = "o ato é registrado com os atributos CONFERIDOS, não com os do catálogo no momento do consumo (ADR-0061)")]
    public async Task AtributosConferidos_VencemOCatalogoNoConsumo()
    {
        CascadingApiFactory api = _fixture.Factory;

        // O catálogo diz que este tipo NÃO congela. A requisição diz que, quando a publicação
        // aconteceu, ele congelava — que é o que a conferência prévia aprovou, e sob o que o
        // 204 foi dado.
        //
        // Se o consumidor relesse o cadastro (que é editável: um admin pode mudar
        // congela_configuracao a qualquer momento), o ato seria registrado como NÃO
        // congelante, e um edital publicado como congelante ficaria com um ato que o
        // contradiz — a divergência voltando depois do 204, quando já não há ninguém para
        // recusá-la. Quem vence é o snapshot.
        await SemearTipoDeAtoAsync(api, "TIPO_MUDOU_NO_CATALOGO", unicoPorObjeto: false, congela: false);

        Guid processoId = (await SemearProcessoAsync(api, nameof(AtributosConferidos_VencemOCatalogoNoConsumo))).ProcessoId;
        Guid atoId = Guid.CreateVersion7();

        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: atoId,
                Orgao: "CEPS",
                Serie: "EDITAL",
                Ano: 2026,
                Numero: "777/2026",
                TipoCodigo: "TIPO_MUDOU_NO_CATALOGO",
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('c', 64),
                Assinante: "Diretor do CEPS",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoId)],
                // O que o catálogo dizia QUANDO a publicação foi aceita.
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(
                    CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false)));
        }

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        AtoNormativo ato = await db.Set<AtoNormativo>().AsNoTracking().SingleAsync(a => a.Id == atoId);

        ato.CongelaConfiguracao.Should().BeTrue(
            "o ato foi publicado sob as regras que valiam então — mudança posterior no cadastro não reescreve o passado");
    }

    [Fact(DisplayName = "vaga do certame já ocupada por OUTRA linhagem recusa a publicação com 422 — antes de escrever")]
    public async Task VagaOcupadaPorOutraLinhagem_RecusaPublicacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(VagaOcupadaPorOutraLinhagem_RecusaPublicacao));

        // Alguém já registrou um ato EDITAL_ABERTURA vinculado a este certame — pelo endpoint
        // administrativo de Publicações, por exemplo, onde os vínculos são opacos. A vaga do
        // objeto está tomada, e ela é monotônica: ocupada, nunca se libera (ADR-0107).
        Guid atoDeOutraLinhagem = Guid.CreateVersion7();
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: atoDeOutraLinhagem,
                Orgao: "CEPS",
                Serie: "EDITAL",
                Ano: 2026,
                Numero: "555/2026",
                TipoCodigo: DadosDoAtoDeTeste.TipoAbertura,
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('d', 64),
                Assinante: "Outro caminho",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoId)],
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(true, true, false)));
        }

        // Publicar agora seria dar 204 e ver o ato morrer na dead letter — o certame ficaria
        // publicado sem ato. A recusa vem antes.
        HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = conferencia.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await selecao.Editais.AsNoTracking().AnyAsync(e => e.ProcessoSeletivoId == processoId))
            .Should().BeFalse("a recusa precede qualquer escrita");
    }

    [Fact(DisplayName = "conflito só no HISTÓRICO (ato registrado antes de o tipo virar único) também recusa a publicação")]
    public async Task ConflitoApenasNoHistorico_RecusaPublicacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(ConflitoApenasNoHistorico_RecusaPublicacao));

        // Um ato registrado quando o tipo AINDA NÃO era único por objeto: não reservou vaga
        // alguma — a tabela de vagas não tem linha para ele.
        Guid atoAntigo = Guid.CreateVersion7();
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: atoAntigo,
                Orgao: "CEPS",
                Serie: "EDITAL",
                Ano: 2026,
                Numero: "444/2026",
                TipoCodigo: DadosDoAtoDeTeste.TipoAbertura,
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('e', 64),
                Assinante: "Registro anterior",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: null,
                MotivoRetificacao: null,
                Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoId)],
                // O tipo NÃO era único quando este ato foi registrado — logo, sem vaga.
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(
                    CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false)));
        }

        await using (AsyncServiceScope conferencia = api.Services.CreateAsyncScope())
        {
            PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
            (await db.Set<LinhagemUnicaPorObjeto>().AsNoTracking().AnyAsync(l => l.EntidadeId == processoId))
                .Should().BeFalse("pré-condição: não há vaga reservada — o tipo não era único quando o ato foi registrado");
        }

        // Agora o tipo É único (o catálogo semeado diz isso). Olhar só a tabela de vagas diria
        // "livre" e a publicação passaria com 204 — e o registro recusaria pelo conflito
        // histórico, deixando o certame publicado sem ato. A conferência pergunta o mesmo que o
        // registro: existe ato deste tipo neste objeto, fora da minha linhagem?
        HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using AsyncServiceScope scopeSelecao = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = scopeSelecao.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await selecao.Editais.AsNoTracking().AnyAsync(e => e.ProcessoSeletivoId == processoId))
            .Should().BeFalse("a recusa precede qualquer escrita");
    }

    [Fact(DisplayName = "ato já retificado por fora recusa a retificação com 422 — a cadeia é linear (ADR-0103)")]
    public async Task AtoJaRetificadoPorFora_RecusaRetificacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoAbertura) = await SemearProcessoAsync(api, nameof(AtoJaRetificadoPorFora_RecusaRetificacao));
        (await PublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid abertura = await ObterEditalIdAsync(api, processoId);
        (await AguardarAtoAsync(api, abertura)).Should().NotBeNull();

        // Alguém já emendou o ato de abertura por fora — pelo endpoint administrativo de
        // Publicações, por exemplo. A cadeia é linear: um ato é retificado no máximo uma vez.
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
                AtoId: Guid.CreateVersion7(),
                Orgao: "CEPS",
                Serie: "EDITAL",
                Ano: 2026,
                Numero: "333/2026",
                TipoCodigo: DadosDoAtoDeTeste.TipoRetificacao,
                DataPublicacao: DateOnly.FromDateTime(DateTime.UtcNow),
                DocumentoHash: new string('f', 64),
                Assinante: "Registro por fora",
                VersaoInvocadaId: null,
                VersaoInvocadaHash: null,
                AtoRetificadoId: abertura,
                MotivoRetificacao: "Retificação registrada fora do fluxo de Seleção",
                Vinculos: [],
                AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(true, false, false)));
        }

        // Retificar agora emendaria um ato já emendado: o registro recusaria com
        // RaizJaRetificada e a retificação ficaria publicada sem ato. A recusa vem antes.
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);
        HttpResponseMessage resposta = await RetificarAsync(client, processoId, documentoRetificacao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        SelecaoDbContext selecao = conferencia.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await selecao.Editais.AsNoTracking()
            .CountAsync(e => e.ProcessoSeletivoId == processoId))
            .Should().Be(1, "a recusa precede qualquer escrita — só o Edital de abertura existe");
    }

    [Fact(DisplayName = "reentrega da mesma requisição não duplica o ato (idempotente por id)")]
    public async Task Reentrega_NaoDuplicaOAto()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(Reentrega_NaoDuplicaOAto));
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid editalId = await ObterEditalIdAsync(api, processoId);
        (await AguardarAtoAsync(api, editalId)).Should().NotBeNull();

        // Reentrega o MESMO envelope, como a fila at-least-once faria após um crash entre
        // o processamento e o ack.
        Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao requisicao = await MontarRequisicaoDoAtoAsync(api, editalId, processoId);
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            Wolverine.IMessageBus bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();
            await bus.InvokeAsync(requisicao);
        }

        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = conferencia.ServiceProvider.GetRequiredService<PublicacoesDbContext>();

        (await db.Set<AtoNormativo>().AsNoTracking().CountAsync(a => a.Id == editalId))
            .Should().Be(1, "o id vem na mensagem — o segundo processamento reencontra o ato e não faz nada");
        (await db.Set<LinhagemUnicaPorObjeto>().AsNoTracking().CountAsync(l => l.EntidadeId == processoId))
            .Should().Be(1, "e não abre uma segunda vaga contra a própria linhagem");
    }

    private static async Task<Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao> MontarRequisicaoDoAtoAsync(
        CascadingApiFactory api, Guid editalId, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        AtoNormativo ato = await db.Set<AtoNormativo>().AsNoTracking().SingleAsync(a => a.Id == editalId);

        return new Unifesspa.UniPlus.Publicacoes.Contracts.RegistrarAtoNormativoRequisicao(
            AtoId: ato.Id,
            Orgao: ato.Orgao,
            Serie: ato.Serie,
            Ano: ato.Ano,
            Numero: ato.Numero,
            TipoCodigo: ato.TipoCodigo,
            DataPublicacao: ato.DataPublicacao,
            DocumentoHash: ato.DocumentoHash,
            Assinante: ato.Assinante,
            VersaoInvocadaId: ato.VersaoInvocada?.Id,
            VersaoInvocadaHash: ato.VersaoInvocada?.Hash,
            AtoRetificadoId: null,
            MotivoRetificacao: null,
            Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoId)],
            AtributosDoTipo: new Unifesspa.UniPlus.Publicacoes.Contracts.AtributosDoTipoAto(
                ato.CongelaConfiguracao, ato.UnicoPorObjeto, ato.EfeitoIrreversivel));
    }

    private static async Task<AtoNormativo?> AguardarAtoAsync(CascadingApiFactory api, Guid atoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.Add(Paciencia);
        while (DateTimeOffset.UtcNow < limite)
        {
            await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
            PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
            AtoNormativo? ato = await db.Set<AtoNormativo>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == atoId);
            if (ato is not null)
            {
                return ato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        return null;
    }

    private static async Task SemearTipoDeAtoAsync(
        CascadingApiFactory api, string codigo, bool unicoPorObjeto, bool congela = true)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            codigo, codigo, congelaConfiguracao: congela, unicoPorObjeto, efeitoIrreversivel: false,
            new DateOnly(2020, 1, 1), null, null).Value!;
        await db.Set<TipoAtoPublicado>().AddAsync(tipo);
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> ObterEditalIdAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.Editais.AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId)
            .Select(e => e.Id)
            .SingleAsync();
    }

    private static async Task<HttpResponseMessage> RetificarAsync(HttpClient client, Guid processoId, Guid documentoId)
    {
        object corpo = new
        {
            motivo = "Correção do prazo de inscrição",
            numero = "001/2026-R1",
            periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            documentoEditalId = documentoId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = DadosDoAtoDeTeste.TipoRetificacao,
            },
        };
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private static async Task<HttpResponseMessage> PublicarAsync(
        HttpClient client, Guid processoId, Guid documentoId, string? tipoAto = null)
    {
        object corpo = new
        {
            numero = "001/2026",
            periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            documentoEditalId = documentoId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = tipoAto ?? DadosDoAtoDeTeste.TipoAbertura,
            },
        };
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
