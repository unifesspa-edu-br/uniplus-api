namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;

/// <summary>
/// Base das suítes dos pedidos de concessão pelo host real. As personas são as do portal: Diogo e Isabella são gestores
/// de acessos (Isabella também exerce a encarregada), Marlene é a encarregada de dados, Betina coordena a equipe do
/// CEPS e Leandro é o beneficiário.
/// </summary>
public abstract class PedidosDeConcessaoTestBase
{
    internal const string Rota = "/api/autorizacao/pedidos-de-concessao";
    internal const string AprovarFuncional = "autorizacao:pedidos:aprovar-funcional";
    internal const string AprovarProtecaoDeDados = "autorizacao:pedidos:aprovar-protecao-de-dados";
    internal const string Administrar = "autorizacao:concessoes:administrar";
    internal const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento especializado.";

    internal static readonly byte[] Pdf = [.. "%PDF-1.7 documento de designacao de teste"u8];

    internal static readonly Persona Diogo = new("diogo.souza", "Diogo Bruno Filipe Souza", "diogo.bruno.souza@uniplus-test.local");
    internal static readonly Persona Isabella = new("isabella.figueiredo", "Isabella Priscila Figueiredo", "isabellapriscilafigueiredo@uniplus-test.local");
    internal static readonly Persona Marlene = new("marlene.martins", "Marlene Marcia Emily Martins", "marlenemarciamartins@uniplus-test.local");
    internal static readonly Persona Betina = new("betina.neves", "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local");
    internal static readonly Persona Leandro = new("leandro.viana", "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local");

    private protected PedidosDeConcessaoTestBase(MonolitoDaInstalacaoFixture fixture) => Fixture = fixture;

    private protected MonolitoDaInstalacaoFixture Fixture { get; }

    internal static Guid SubDe(string username) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"persona:{username}"))[..16]);

    internal static Identidade IdentidadeDe(Persona p) => new(TestAuthHandler.DefaultIssuer, p.Sub.ToString());

    private protected HttpClient Cliente(Persona persona, string? email = null, string emailVerificado = "true", bool semJti = false, params string[] permissoes)
    {
        HttpClient client = Fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, persona.Sub.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, persona.Nome);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email ?? persona.Email);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, emailVerificado);
        if (semJti)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.SemJtiHeader, "1");
        }

        if (permissoes.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
        }

        return client;
    }

    private protected HttpClient ClienteComPermissoes(Persona persona, params string[] permissoes) =>
        Cliente(persona, null, "true", false, permissoes);

    internal static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        return await client.SendAsync(request);
    }

    internal static async Task<JsonElement> LerAsync(HttpClient client, string url)
    {
        using HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    internal static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        string texto = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(esperado, texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>Confere a recusa pelo status e pelo código público do catálogo de erros.</summary>
    internal static async Task ConferirRecusaAsync(HttpResponseMessage resposta, HttpStatusCode status, string motivo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.pedido.{motivo}");
    }

    internal static async Task<Guid> CriarAsync(
        HttpClient solicitante, Guid perfilId, Guid? unidadeId, Persona? beneficiario, string justificativa = Justificativa)
    {
        using HttpResponseMessage resposta = await CriarBrutoAsync(solicitante, perfilId, unidadeId, beneficiario?.Email, justificativa);
        JsonElement corpo = await CorpoAsync(resposta, HttpStatusCode.Created);
        return corpo.GetProperty("id").GetGuid();
    }

    internal static Task<HttpResponseMessage> CriarBrutoAsync(
        HttpClient solicitante, Guid perfilId, Guid? unidadeId, string? emailDoBeneficiario, string? justificativa = Justificativa) =>
        EnviarAsync(solicitante, HttpMethod.Post, Rota, new { beneficiarioEmail = emailDoBeneficiario, perfilId, unidadeId, justificativa });

    internal static async Task<string> SituacaoAsync(HttpClient client, Guid id) =>
        (await LerAsync(client, $"{Rota}/{id}")).GetProperty("situacao").GetString()!;

    internal static async Task<string[]> TiposDaTrilhaAsync(HttpClient client, Guid id)
    {
        JsonElement eventos = await LerAsync(client, $"{Rota}/{id}/trilha");
        return [.. eventos.EnumerateArray().Select(e => e.GetProperty("tipo").GetString()!)];
    }

    private protected async Task<Concessao[]> ConcessoesAsync(Persona beneficiario, Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        string sub = beneficiario.Sub.ToString();
        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Concessoes.AsNoTracking()
            .Where(c => c.BeneficiarioSub == sub && c.PerfilId == perfilId)
            .ToArrayAsync();
    }

    private protected async Task ResetarAsync()
    {
        await Fixture.ProvisionarBucketsAsync();
        await ExecutarSqlAsync("DELETE FROM autorizacao.pedido_anexo_leituras");
        await ExecutarSqlAsync("DELETE FROM autorizacao.pedido_decisoes");
        await ExecutarSqlAsync("DELETE FROM autorizacao.pedido_anexos");
        await ExecutarSqlAsync("DELETE FROM autorizacao.pedidos_de_concessao");
        await ExecutarSqlAsync(
            "DELETE FROM autorizacao.concessoes WHERE origem_tipo = 'pedido' OR perfil_id IN ({0}, {1})",
            PerfisDeSistema.GestorDeAcessosId,
            PerfisDeSistema.EncarregadaDeDadosId);
    }

    private protected async Task ExecutarSqlAsync(string sql, params object[] parametros)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sql, parametros);
    }

    /// <summary>Semeia um perfil comum. O código fora do catálogo conta como o mais restrito (sensível).</summary>
    private protected async Task<Guid> SemearPerfilAsync(string prefixo, params string[] permissoes)
    {
        Perfil perfil = Perfil.Criar($"{prefixo} {Guid.NewGuid():N}", "Perfil semeado pelo teste.", permissoes, new CatalogoPermissivo()).Value!;
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        db.Perfis.Add(perfil);
        await db.SaveChangesAsync();
        return perfil.Id;
    }

    private protected Task<Guid> SemearPerfilInternoAsync() =>
        SemearPerfilAsync("Acompanhamento de números", "autorizacao:permissoes:consultar-catalogo");

    private protected Task<Guid> SemearPerfilSensivelAsync() =>
        SemearPerfilAsync("Análise de atendimento", "selecao:atendimento:analisar");

    private protected async Task<Guid> SemearUnidadeAsync()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        Unidade unidade = Unidade.Criar(
            nome: $"Centro de Estudos {sufixo}",
            alias: null,
            slug: $"ceps-{sufixo}",
            sigla: $"C{sufixo}".ToUpperInvariant(),
            codigo: $"PD{sufixo}".ToUpperInvariant(),
            unidadeSuperiorId: null,
            tipo: TipoUnidade.Centro,
            unidadeAcademica: true,
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null).Value!;

        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext db = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    private protected async Task SemearConcessaoAsync(Persona beneficiario, Guid perfilId, EscopoDaConcessao escopo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        IConcessaoRepository repositorio = scope.ServiceProvider.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Concessao concessao = Concessao.Conceder(
                IdentidadeDe(beneficiario),
                perfil,
                escopo,
                new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
                IdentidadeDe(Diogo),
                DateTimeOffset.UtcNow)
            .Value!;
        await repositorio.AdicionarAsync(concessao);
        await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();
    }

    /// <summary>Sobe o PDF pela URL de PUT assinada e devolve o ETag.</summary>
    internal static async Task<string> EnviarPdfAsync(Uri url)
    {
        using HttpClient http = new();
        using ByteArrayContent conteudo = new(Pdf);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using HttpResponseMessage put = await http.PutAsync(url, conteudo);
        put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return put.Headers.ETag!.Tag;
    }

    /// <summary>Entrega à fila de resultados da Autorização o veredicto que o verificador publicaria.</summary>
    private protected async Task InjetarResultadoAsync(Guid arquivoId, VeredictoDaVerificacao veredicto)
    {
        bool verificado = veredicto == VeredictoDaVerificacao.Verificado;
        ResultadoDaVerificacaoDeArquivo resultado = new(
            arquivoId,
            veredicto,
            verificado ? MotivoDaRecusa.NaoSeAplica : MotivoDaRecusa.PdfComSenha,
            verificado ? new string('a', 64) : null,
            verificado ? Pdf.Length : null,
            verificado ? FormatoDoArquivo.Pdf : null);

        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .EndpointFor(FilasDaVerificacao.ResultadosAutorizacao).SendAsync(resultado);
    }

    internal static async Task AguardarAsync(Func<Task<bool>> condicao, string falha)
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

    /// <summary>Anexa o PDF ao pedido e leva-o a "verificado" pelo caminho real: URL assinada, confirmação e fila.</summary>
    private protected async Task<Guid> AnexarVerificadoAsync(HttpClient solicitante, Guid pedidoId)
    {
        Guid anexoId = await AnexarEmVerificacaoAsync(solicitante, pedidoId);
        await InjetarResultadoAsync(anexoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(
            async () => (await SituacaoDoAnexoAsync(solicitante, pedidoId, anexoId)) == "verificado",
            $"o anexo {anexoId} não chegou a verificado");
        return anexoId;
    }

    internal static async Task<Guid> AnexarEmVerificacaoAsync(HttpClient solicitante, Guid pedidoId)
    {
        using HttpResponseMessage envio = await EnviarAsync(solicitante, HttpMethod.Post, $"{Rota}/{pedidoId}/anexos", new { tamanhoDoPdf = Pdf.Length });
        JsonElement corpo = await CorpoAsync(envio, HttpStatusCode.Created);
        corpo.GetProperty("tipoDeMidia").GetString().Should().Be("application/pdf");
        Guid anexoId = corpo.GetProperty("id").GetGuid();
        string etag = await EnviarPdfAsync(new Uri(corpo.GetProperty("urlDeEnvio").GetString()!, UriKind.Absolute));

        using HttpResponseMessage confirmacao = await EnviarAsync(
            solicitante, HttpMethod.Post, $"{Rota}/{pedidoId}/anexos/{anexoId}/confirmacao", new { etag });
        (await CorpoAsync(confirmacao, HttpStatusCode.OK)).GetProperty("situacao").GetString().Should().Be("em_verificacao");
        return anexoId;
    }

    internal static async Task<string> SituacaoDoAnexoAsync(HttpClient client, Guid pedidoId, Guid anexoId)
    {
        JsonElement pedido = await LerAsync(client, $"{Rota}/{pedidoId}");
        return pedido.GetProperty("anexos").EnumerateArray()
            .Single(a => a.GetProperty("id").GetGuid() == anexoId).GetProperty("situacao").GetString()!;
    }

    private protected string LerLog()
    {
        if (!File.Exists(Fixture.ArquivoDeLog))
        {
            return string.Empty;
        }

        using FileStream arquivo = new(Fixture.ArquivoDeLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader leitor = new(arquivo);
        return leitor.ReadToEnd();
    }

    internal sealed record Persona(string Username, string Nome, string Email)
    {
        public Guid Sub { get; } = SubDe(Username);
    }

    private sealed class CatalogoPermissivo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;
    }
}
