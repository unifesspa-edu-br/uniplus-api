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
/// SPIKE #820 — prova, contra o host real (Wolverine + PG queue durável + Postgres via
/// Testcontainers), as quatro propriedades que decidem o desenho da orquestração
/// Seleção → Publicações. Nenhuma delas foi assumida: cada uma é medida.
/// </summary>
/// <remarks>
/// O que se está tentando derrubar: que a publicação e o registro do ato possam divergir
/// de um jeito que trave o certame. A v1 do plano (chamada síncrona, ato primeiro) tinha
/// esse defeito — um ato órfão reserva a vaga de linhagem do objeto (ADR-0107) e o
/// certame nunca mais publica. A v2 (transação compartilhada) é inviável no Wolverine.
/// Esta é a v3: a requisição do ato viaja no outbox, na mesma transação do Edital.
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class RegistroDeAtoPorFilaDuravelSpikeTests
{
    private static readonly TimeSpan Paciencia = TimeSpan.FromSeconds(30);

    private readonly CascadingFixture _fixture;

    public RegistroDeAtoPorFilaDuravelSpikeTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "SPIKE 820 · S1 — publicar enfileira o ato no outbox e Publicações o registra depois do commit")]
    public async Task Publicar_RegistraAtoEmPublicacoes_PelaFilaDuravel()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await SemearTipoDeAtoAsync(api, "EDITAL_ABERTURA", unicoPorObjeto: true);
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

    [Fact(DisplayName = "SPIKE 820 · S2 — o registro recusado NÃO reserva a vaga do certame (o defeito que condenou o desenho síncrono)")]
    public async Task RegistroRecusado_NaoPrendeAVagaDoCertame()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await SemearTipoDeAtoAsync(api, "EDITAL_ABERTURA", unicoPorObjeto: true);
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
                Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoOutro)]));
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

    [Fact(DisplayName = "SPIKE 820 · S3 — reentrega da mesma requisição não duplica o ato (idempotente por id)")]
    public async Task Reentrega_NaoDuplicaOAto()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await SemearTipoDeAtoAsync(api, "EDITAL_ABERTURA", unicoPorObjeto: true);
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
            Vinculos: [new Unifesspa.UniPlus.Publicacoes.Contracts.VinculoEntidadeRequisicao("PROCESSO_SELETIVO", processoId)]);
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

    private static async Task SemearTipoDeAtoAsync(CascadingApiFactory api, string codigo, bool unicoPorObjeto)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            codigo, codigo, congelaConfiguracao: true, unicoPorObjeto, efeitoIrreversivel: false,
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

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoId)
    {
        object corpo = new
        {
            numero = "001/2026",
            periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            documentoEditalId = documentoId,
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
