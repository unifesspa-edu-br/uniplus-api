namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A solicitação da proteção do catálogo, no host real, com a decisão de acesso e a leitura dos destinatários dentro do
/// handler: o verificador e os titulares leem o <c>AutorizacaoDbContext</c>, e o handler continua com um único contexto
/// transacional, o da Configuração. Se o detector do Wolverine visse os dois contextos, o handler nem seria gerado; e a
/// escrita e o aviso por e-mail, que o handler devolve como mensagem, só chegam juntos quando a transação do outbox é a mesma
/// da escrita.
/// </summary>
[Collection(MonolitoComMailpitCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class SolicitacaoDaProtecaoNoHostTests
{
    private const string Base = "/api/configuracao/admin/protecao-do-catalogo";
    private const string Leandro = "leandro.viana";
    private const string Caroline = "caroline.lima";
    private const string EmailDeCaroline = "caroline.lima@unifesspa.edu.br";

    private readonly MonolitoComMailpitFixture _fixture;

    public SolicitacaoDaProtecaoNoHostTests(MonolitoComMailpitFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Quem mantém o cadastro solicita; a escrita e o aviso por e-mail à aprovadora saem juntos, com um único contexto transacional")]
    public async Task Solicitar_DecideNoHandler_EscritaEAvisoAtomicos()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, Leandro);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, Caroline);
        await ConcessoesDeTeste.ConcederAsync(
            servicos, Leandro, await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoConfiguracao.TiposDocumentoManter.Codigo));
        await ConcessoesDeTeste.ConcederAsync(
            servicos, Caroline, await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo),
            email: EmailDeCaroline);

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid pendente = await CadastrarTipoPendenteAsync(client);

        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, $"{Base}/solicitacoes",
            new { itens = new[] { new { cadastro = "TIPO_DOCUMENTO", id = pendente } } });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tiposDeDocumento").GetInt32().Should().Be(1);
        (await ListarAsync(client)).Single(i => i.GetProperty("id").GetGuid() == pendente)
            .GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("SOLICITADA");
        await AguardarEmailPara(EmailDeCaroline, TimeSpan.FromSeconds(30));
    }

    [Fact(DisplayName = "Falha no commit, com o aviso já gravado no outbox: nada fica gravado, o item segue pendente e nenhum e-mail sai (a escrita e o outbox são a mesma transação)")]
    public async Task Solicitar_FalhaNoCommit_NadaPersistidoNemAvisado()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        string emailDoAprovador = $"aprovadora.{Guid.NewGuid():N}@unifesspa.edu.br";
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, Leandro);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(servicos, Caroline);
        await ConcessoesDeTeste.ConcederAsync(
            servicos, Leandro, await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoConfiguracao.TiposDocumentoManter.Codigo));
        await ConcessoesDeTeste.ConcederAsync(
            servicos, Caroline, await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo),
            email: emailDoAprovador);

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid pendente = await CadastrarTipoPendenteAsync(client);
        int disparosAntes = _fixture.Factory.FalhaNoCommit.Disparos;
        int envelopesAntes = await EnvelopesDeEmailNoOutboxAsync();
        _fixture.Factory.FalhaNoCommit.Armar();

        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, $"{Base}/solicitacoes",
            new { itens = new[] { new { cadastro = "TIPO_DOCUMENTO", id = pendente } } });

        _fixture.Factory.FalhaNoCommit.Disparos.Should().Be(disparosAntes + 1);
        _fixture.Factory.FalhaNoCommit.EnvelopesNaTransacao.Should().BeGreaterThan(
            envelopesAntes, "o aviso já estava gravado no outbox, na transação, quando o commit falhou");
        resposta.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await ListarAsync(client)).Single(i => i.GetProperty("id").GetGuid() == pendente)
            .GetProperty("aprovacao").GetProperty("situacao").GetString().Should().Be("PENDENTE", "a escrita foi desfeita");
        (await EnvelopesDeEmailNoOutboxAsync()).Should().BeLessThanOrEqualTo(envelopesAntes, "o envelope do aviso foi desfeito com a transação");
        (await HaEmailParaAsync(emailDoAprovador, TimeSpan.FromSeconds(8))).Should().BeFalse("o aviso estava na mesma transação da escrita");
    }

    [Fact(DisplayName = "Quem não mantém nenhum cadastro é recusado pelo handler com 403, e nada é avisado")]
    public async Task Solicitar_SemPermissao_Recusa()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, $"{Base}/solicitacoes",
            new { itens = new[] { new { cadastro = "TIPO_DOCUMENTO", id = Guid.CreateVersion7() } } });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CadastrarTipoPendenteAsync(HttpClient client)
    {
        using HttpResponseMessage criar = await EnviarAsync(client, HttpMethod.Post, "/api/configuracao/admin/tipos-documento", new
        {
            codigo = $"TIPO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}",
            nome = "Laudo médico complementar",
            categoria = "SAUDE",
            classificacaoProtecao = "INTERNO",
            finalidadeTratamento = "Comprovação documental do candidato.",
            hipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);

        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<JsonElement>> ListarAsync(HttpClient client)
    {
        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, Base);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo = null)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.UserIdHeader, Leandro);
        if (metodo != HttpMethod.Get)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request);
    }

    private async Task<int> EnvelopesDeEmailNoOutboxAsync()
    {
        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        ConfiguracaoDbContext contexto = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        return await contexto.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_outgoing_envelopes WHERE message_type ILIKE '%email%'")
            .SingleAsync();
    }

    private async Task<bool> HaEmailParaAsync(string destinatario, TimeSpan limite)
    {
        try
        {
            await AguardarEmailPara(destinatario, limite);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task AguardarEmailPara(string destinatario, TimeSpan limite)
    {
        using HttpClient mailpit = new() { BaseAddress = _fixture.ApiDoMailpit };
        DateTime fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages");
            if (lista.GetProperty("messages").EnumerateArray().Any(m =>
                m.GetProperty("To").EnumerateArray().Any(t => t.GetProperty("Address").GetString() == destinatario)))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"O aviso para '{destinatario}' não chegou ao Mailpit em {limite}.");
    }
}
