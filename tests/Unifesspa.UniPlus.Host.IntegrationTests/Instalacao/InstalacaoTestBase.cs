namespace Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Minio;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;
using Unifesspa.UniPlus.Verificador.Verificacao;

using Wolverine;
using Wolverine.Runtime.Handlers;


/// <summary>
/// O apoio dos testes da instalação dos primeiros gestores de acessos pelo host real: Postgres, RabbitMQ, MinIO e Mailpit
/// em contêineres. O PDF sobe pela URL assinada e o veredicto do verificador é injetado na fila de resultados da
/// Autorização, como o verificador o publica. As personas são as do portal: Paulo Conceição executa, Manoel Aparício
/// também tem o papel, e Diogo, Isabella e Betina são os gestores designados.
/// </summary>
public abstract partial class InstalacaoTestBase
{
    private protected const string Rota = "/api/autorizacao/instalacao";
    private protected const string PapelDeInstalacao = "uniplus-instalacao";
    private protected const string Numero = "SIPAC 4521/2026";

    private protected static readonly byte[] Pdf = [.. "%PDF-1.7 documento de designacao de teste"u8];

    private protected static readonly Persona Paulo = new("paulo.conceicao", "paulo-daconceicao93@uniplus-test.local");
    private protected static readonly Persona Manoel = new("manoel.aparicio", "manoel.guilherme.aparicio@uniplus-test.local");
    private protected static readonly Persona Diogo = new("diogo.souza", "diogo.bruno.souza@uniplus-test.local");
    private protected static readonly Persona Isabella = new("isabella.figueiredo", "isabella.figueiredo@uniplus-test.local");
    private protected static readonly Persona Betina = new("betina.neves", "betina.neves@uniplus-test.local");

    private protected InstalacaoTestBase(MonolitoDaInstalacaoFixture fixture) => Fixture = fixture;

    private protected MonolitoDaInstalacaoFixture Fixture { get; }

    private protected static Guid SubDe(string username) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"persona:{username}"))[..16]);

    // Quem lê a trilha administra concessões por concessão gravada. A proteção de dados serve: a instalação só conta e
    // apaga as concessões do perfil do gestor.
    private protected async Task<HttpClient> ClienteQueLeATrilhaAsync()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(
            Fixture.Factory.Services, Isabella.Sub.ToString(), TipoDeGovernanca.RepresentanteDoControlador);
        return Cliente(Isabella, papel: false);
    }

    private protected HttpClient Cliente(Persona persona, bool papel = true, string? email = null, string emailVerificado = "true")
    {
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, persona.Sub.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email ?? persona.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, emailVerificado);
        if (papel)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, PapelDeInstalacao);
        }

        return client;
    }

    private protected static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null, string? chave = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    private protected static async Task<JsonElement> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private protected static async Task<HttpResponseMessage> IniciarBrutoAsync(
        HttpClient client, Persona primeiro, Persona segundo, string numero = Numero, long? tamanho = null) =>
        await EnviarAsync(client, HttpMethod.Post, Rota, new
        {
            gestores = new[] { Identidade(primeiro), Identidade(segundo) },
            numeroDoDocumento = numero,
            tamanhoDoPdf = tamanho ?? Pdf.Length,
        });

    private protected static object Identidade(Persona p) => new { emissor = TestAuthHandler.DefaultIssuer, sub = p.Sub.ToString(), email = p.Email, nome = p.Username };

    private protected static async Task<(Guid Id, Uri UrlDeEnvio)> IniciarAsync(HttpClient client, Persona primeiro, Persona segundo, long? tamanho = null)
    {
        using HttpResponseMessage resposta = await IniciarBrutoAsync(client, primeiro, segundo, tamanho: tamanho);
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        JsonElement corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        corpo.GetProperty("tipoDeMidia").GetString().Should().Be("application/pdf");
        return (corpo.GetProperty("id").GetGuid(), new Uri(corpo.GetProperty("urlDeEnvio").GetString()!, UriKind.Absolute));
    }

    private protected static Task<string> EnviarPdfAsync(Uri url) => EnviarPdfAsync(url, Pdf);

    private protected static async Task<string> EnviarPdfAsync(Uri url, byte[] bytes)
    {
        using HttpClient http = new();
        using ByteArrayContent conteudo = new(bytes);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using HttpResponseMessage put = await http.PutAsync(url, conteudo);
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return put.Headers.ETag!.Tag;
    }

    private protected async Task InjetarResultadoAsync(
        Guid id, VeredictoDaVerificacao veredicto, MotivoDaRecusa motivo = MotivoDaRecusa.NaoSeAplica, string fila = FilasDaVerificacao.ResultadosAutorizacao)
    {
        bool verificado = veredicto == VeredictoDaVerificacao.Verificado;
        ResultadoDaVerificacaoDeArquivo resultado = new(
            id, veredicto, motivo, verificado ? new string('a', 64) : null, verificado ? Pdf.Length : null, verificado ? FormatoDoArquivo.Pdf : null);

        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().EndpointFor(fila).SendAsync(resultado);
    }

    private protected static async Task AguardarSituacaoAsync(HttpClient client, Guid id, string situacao)
    {
        await AguardarAsync(
            async () => (await LerAsync(client, $"{Rota}/{id}")).GetProperty("situacao").GetString() == situacao,
            $"a instalação {id} não chegou a {situacao}");
    }

    private protected static async Task AguardarAsync(Func<Task<bool>> condicao, string falha)
    {
        DateTime fim = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < fim)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(falha);
    }

    private protected async Task<Guid> ChegarAoDocumentoVerificadoAsync(HttpClient executor)
    {
        (Guid id, Uri url) = await IniciarAsync(executor, Diogo, Isabella);
        string etag = await EnviarPdfAsync(url);
        (await EnviarAsync(executor, HttpMethod.Post, $"{Rota}/{id}/documento/confirmacao", new { etag })).StatusCode.Should().Be(HttpStatusCode.OK);
        await InjetarResultadoAsync(id, VeredictoDaVerificacao.Verificado);
        await AguardarSituacaoAsync(executor, id, "documento_verificado");
        return id;
    }

    private protected async Task<Guid> ChegarAoCodigoEnviadoAsync(HttpClient executor)
    {
        Guid id = await ChegarAoDocumentoVerificadoAsync(executor);
        await PedirCodigoAsync(executor, id, Paulo);
        return id;
    }

    private protected async Task<string> PedirCodigoAsync(HttpClient executor, Guid id, Persona destinatario)
    {
        await LimparCaixaAsync();
        using HttpResponseMessage resposta = await EnviarAsync(executor, HttpMethod.Post, $"{Rota}/{id}/codigo");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        (await resposta.Content.ReadAsStringAsync()).Should().NotMatchRegex("(?<![0-9a-f])\\d{8}(?![0-9a-f])", "a resposta nunca traz o código");
        return await UltimoCodigoAsync(destinatario);
    }

    private protected async Task<string> UltimoCodigoAsync(Persona destinatario)
    {
        string? texto = null;
        await AguardarAsync(
            async () =>
            {
                JsonElement[] mensagens = await MensagensPara(destinatario);
                texto = mensagens.Length == 0 ? null : mensagens[0].GetProperty("Text").GetString();
                return texto is not null;
            },
            "o e-mail com o código não chegou ao Mailpit");

        Match codigo = CodigoNoEmail().Match(texto!);
        codigo.Success.Should().BeTrue();
        return codigo.Groups[1].Value;
    }

    [GeneratedRegex("código de confirmação é (\\d{8})\\.")]
    private protected static partial Regex CodigoNoEmail();

    private protected async Task<JsonElement[]> MensagensPara(Persona destinatario)
    {
        using HttpClient mailpit = new() { BaseAddress = Fixture.ApiDoMailpit };
        JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        List<JsonElement> completas = [];
        IEnumerable<JsonElement> doDestinatario = lista.GetProperty("messages").EnumerateArray()
            .Where(resumo => resumo.GetProperty("To").EnumerateArray().Any(t => string.Equals(
                t.GetProperty("Address").GetString(), destinatario.Email, StringComparison.OrdinalIgnoreCase)));
        foreach (JsonElement resumo in doDestinatario)
        {
            string idDaMensagem = resumo.GetProperty("ID").GetString()!;
            completas.Add(await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{idDaMensagem}"));
        }

        return [.. completas];
    }

    private protected async Task LimparCaixaAsync()
    {
        using HttpClient mailpit = new() { BaseAddress = Fixture.ApiDoMailpit };
        using HttpResponseMessage resposta = await mailpit.DeleteAsync(new Uri("/api/v1/messages", UriKind.Relative));
        resposta.EnsureSuccessStatusCode();
    }

    private protected static async Task ConferirRecusaAsync(HttpResponseMessage resposta, string motivo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        corpo.Should().Contain($"uniplus.autorizacao.instalacao.{motivo}");
    }

    private protected async Task<string[]> ListarGestoresAtivosAsync()
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        return await db.Concessoes.AsNoTracking()
            .Where(c => c.PerfilId == PerfisDeSistema.GestorDeAcessosId && c.RevogadaEm == null)
            .Select(c => c.BeneficiarioSub)
            .ToArrayAsync();
    }

    private protected async Task ResetarAsync()
    {
        await Fixture.ProvisionarBucketsAsync();
        await ExecutarSqlAsync("DELETE FROM autorizacao.revisao_itens");
        await ExecutarSqlAsync("DELETE FROM autorizacao.revisoes_de_acessos");
        await ExecutarSqlAsync("DELETE FROM autorizacao.liberacoes_da_reclassificacao");
        await ExecutarSqlAsync("DELETE FROM autorizacao.bases_da_permissao_na_concessao");
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.concessoes WHERE perfil_id = {0}", PerfisDeSistema.GestorDeAcessosId);
        await ExecutarSqlAsync("DELETE FROM autorizacao.instalacao_tentativas");
        await ExecutarSqlAsync("DELETE FROM autorizacao.instalacoes");
        await LimparCaixaAsync();
    }

    private protected async Task ExecutarSqlAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }

    private protected async Task SemearGestorAtivoAsync(Persona gestor)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarConcessaoDeGestorAsync(scope, gestor, new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()));
        await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    // Uma concessão revogada da mesma origem e do mesmo beneficiário é o que faz a segunda gravação da confirmação
    // violar o índice único de origem.
    private protected async Task SemearConcessaoRevogadaDaOrigemAsync(Guid instalacaoId, Persona gestor)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        Concessao concessao = await MontarConcessaoDeGestorAsync(scope, gestor, new OrigemDaConcessao(TipoDeOrigem.Instalacao, instalacaoId));
        concessao.Revogar(new Identidade(TestAuthHandler.DefaultIssuer, Diogo.Sub.ToString()), "Semente do teste.", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<IConcessaoRepository>().AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    private protected static async Task<Concessao> MontarConcessaoDeGestorAsync(AsyncServiceScope scope, Persona gestor, OrigemDaConcessao origem)
    {
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(PerfisDeSistema.GestorDeAcessosId))!;
        return Concessao.Conceder(
                new Identidade(TestAuthHandler.DefaultIssuer, gestor.Sub.ToString()),
                perfil,
                EscopoDaConcessao.DaInstituicao(),
                origem,
                new Identidade(TestAuthHandler.DefaultIssuer, Paulo.Sub.ToString()),
                DateTimeOffset.UtcNow,
                scope.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Value!;
    }

    private protected async Task<string[]> TiposDaTrilhaDaInstalacaoAsync(Guid id)
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/instalacoes/{id}/trilha");
        return [.. eventos.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
    }

    private protected async Task<Dictionary<string, DateTimeOffset>> InstantesDaTrilhaDaInstalacaoAsync(Guid id)
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/instalacoes/{id}/trilha");
        return eventos.EnumerateArray().ToDictionary(e => e.GetProperty("tipo").GetString()!, e => e.GetProperty("instante").GetDateTimeOffset());
    }

    private protected async Task<string[]> TiposDaTrilhaDaConcessaoAsync(Guid instalacaoId, Persona beneficiario)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        string sub = beneficiario.Sub.ToString();
        Guid concessao = await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes.AsNoTracking()
            .Where(c => c.OrigemId == instalacaoId && c.BeneficiarioSub == sub)
            .Select(c => c.Id)
            .SingleAsync();

        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement eventos = await LerAsync(gestor, $"/api/autorizacao/concessoes/{concessao}/trilha");
        return [.. eventos.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
    }

    private protected async Task<(string Autor, string Resultado)[]> ListarTentativasSemPendenciaAsync()
    {
        using HttpClient gestor = await ClienteQueLeATrilhaAsync();
        JsonElement pagina = await LerAsync(gestor, "/api/autorizacao/instalacoes/tentativas-sem-pendencia");
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
        return [.. itens.EnumerateArray().Select(t => (
            t.GetProperty("autor").GetProperty("sub").GetString()!,
            t.GetProperty("resultado").GetString()!))];
    }

    private protected string LerLog()
    {
        if (!File.Exists(Fixture.ArquivoDeLog))
        {
            return string.Empty;
        }

        using FileStream arquivo = new(Fixture.ArquivoDeLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader leitor = new(arquivo);

        // O Serilog escreve o Guid entre aspas; sem elas o trecho esperado é o do modelo da mensagem.
        return leitor.ReadToEnd().Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    private protected async Task AguardarLogAsync(string trecho) =>
        await AguardarAsync(() => Task.FromResult(LerLog().Contains(trecho, StringComparison.Ordinal)), $"o log não registrou \"{trecho}\"");

    private protected sealed record Persona(string Username, string Email)
    {
        public Guid Sub { get; } = SubDe(Username);
    }
}
