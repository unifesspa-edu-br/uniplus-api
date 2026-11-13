namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Host.IntegrationTests.Perfis;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Base das suítes da providência pelo host real, com transações reais. Cecília Moura é a Encarregada de dados que
/// solicita; Diogo, Isabella e Betina são gestores de acessos, e Marlene é Representante do controlador; Leandro é o
/// beneficiário, com o e-mail dele gravado na concessão.
/// </summary>
public abstract class ProvidenciasNoHostTestBase : PedidosDeConcessaoTestBase
{
    internal const string RotaDasProvidencias = "/api/autorizacao/providencias";
    internal const string Fundamento = "O servidor deixou a banca do edital 12/2026 e mantém acesso a documentos sensíveis.";
    internal const string Motivo = "O servidor deixou a banca do edital 12/2026.";

    /// <summary>A trava que a revogação, por qualquer caminho, disputa com a providência.</summary>
    internal const string TravaDaConcessao = "SELECT 1 FROM autorizacao.concessoes WHERE id = {0} FOR UPDATE";

    /// <summary>A linha que as respostas simultâneas disputam no <c>UPDATE</c>.</summary>
    internal const string TravaDaProvidencia = "SELECT 1 FROM autorizacao.providencias WHERE id = {0} FOR UPDATE";

    internal static readonly Persona Cecilia = new("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");

    private protected ProvidenciasNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>Reinicia o ambiente com três gestores de acessos, a Representante e a Encarregada, e devolve o cliente de cada um.</summary>
    private protected async Task<Governanca> GovernancaAsync()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Cecilia.Email);
        return new Governanca(
            await ClienteComPermissoesAsync(Diogo, Administrar), await ClienteComPermissoesAsync(Isabella, Administrar),
            await ClienteComPermissoesAsync(Betina, Administrar), await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados), Cliente(Cecilia));
    }

    /// <summary>A concessão de Leandro num perfil sensível novo, com o e-mail dele e o fim da vigência informado.</summary>
    private protected async Task<Guid> ConcessaoDeLeandroAsync(DateTimeOffset? validaAte = null) =>
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), await SemearPerfilSensivelAsync(), EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()), validaAte, Leandro.Email);

    private protected static async Task<Guid> SolicitarAsync(HttpClient encarregada, Guid concessaoId)
    {
        using HttpResponseMessage resposta = await SolicitarBrutoAsync(encarregada, concessaoId);
        return (await CorpoAsync(resposta, HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }

    private protected static Task<HttpResponseMessage> SolicitarBrutoAsync(HttpClient cliente, Guid concessaoId, string tipo = "retirada") =>
        EnviarAsync(cliente, HttpMethod.Post, RotaDasProvidencias, new { concessaoId, tipo, fundamento = Fundamento });

    private protected static Task<HttpResponseMessage> ResponderAsync(HttpClient cliente, Guid providenciaId, string motivo = "O acesso segue necessário até o fim do recurso.") =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaDasProvidencias}/{providenciaId}/resposta", new { motivo });

    private protected static Task<HttpResponseMessage> AtenderAsync(HttpClient cliente, Guid providenciaId, string? confirmacaoEmail, string? chave = null, string motivo = Motivo) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaDasProvidencias}/{providenciaId}/atendimento", new { motivo, confirmacaoEmail }, chave);

    private protected static Task<JsonElement> ProvidenciaAsync(HttpClient cliente, Guid providenciaId) =>
        LerAsync(cliente, $"{RotaDasProvidencias}/{providenciaId}");

    private protected static async Task<Guid[]> FilaDasProvidenciasAsync(HttpClient cliente, string consulta = "")
    {
        JsonElement pagina = await LerAsync(cliente, RotaDasProvidencias + consulta);
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
        return [.. itens.EnumerateArray().Select(static i => i.GetProperty("id").GetGuid())];
    }

    /// <summary>Confere a recusa pelo status e pelo código público, do catálogo de erros ou o comum de concorrência.</summary>
    private protected static async Task ConferirCodigoAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain(codigo);
    }

    /// <summary>A linha gravada da providência: o estado, a versão de sistema e o instante da última alteração.</summary>
    private protected async Task<LinhaDaProvidencia> LinhaAsync(Guid providenciaId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<LinhaDaProvidencia>($"SELECT estado AS \"Estado\", xmin::text AS \"Versao\", updated_at AS \"AlteradaEm\", motivo_do_desfecho AS \"Motivo\" FROM autorizacao.providencias WHERE id = {providenciaId}")
            .SingleAsync();
    }

    /// <summary>Quantos avisos do tipo foram registrados para a providência.</summary>
    private protected async Task<int> AvisosEmitidosAsync(Guid providenciaId, string tipo)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.avisos_emitidos WHERE fato_id = {providenciaId} AND tipo = {tipo}")
            .SingleAsync();
    }

    /// <summary>
    /// Segura a linha numa transação do teste (<see cref="TravaDaConcessao"/> ou <see cref="TravaDaProvidencia"/>), dispara
    /// as chamadas na ordem, esperando o banco registrar cada uma bloqueada, e só então solta a linha.
    /// </summary>
    private protected async Task<HttpResponseMessage[]> EmFilaSobATravaDaLinhaAsync(string sqlDaTrava, Guid id, params Func<Task<HttpResponseMessage>>[] chamadas)
    {
        await using TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        await trava.Servicos.GetRequiredService<AutorizacaoDbContext>().Database.ExecuteSqlRawAsync(sqlDaTrava, id);

        List<Task<HttpResponseMessage>> disparadas = [];
        foreach (Func<Task<HttpResponseMessage>> chamada in chamadas)
        {
            disparadas.Add(chamada());
            await trava.AguardarBloqueioAsync(disparadas.Count);
        }

        await trava.ConfirmarAsync();
        return await Task.WhenAll(disparadas);
    }

    /// <summary>Os clientes da governança reiniciada.</summary>
    private protected sealed record Governanca(HttpClient Diogo, HttpClient Isabella, HttpClient Betina, HttpClient Marlene, HttpClient Cecilia);

    /// <summary>A linha gravada da providência, lida direto do banco.</summary>
    private protected sealed record LinhaDaProvidencia(string Estado, string Versao, DateTimeOffset? AlteradaEm, string? Motivo);
}
