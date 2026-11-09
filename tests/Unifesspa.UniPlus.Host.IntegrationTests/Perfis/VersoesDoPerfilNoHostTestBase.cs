namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>Chamadas, leituras e acessos ao banco comuns às suítes da versão do perfil pelo host real.</summary>
public abstract class VersoesDoPerfilNoHostTestBase : PedidosDeConcessaoTestBase
{
    private protected const string RotaPerfis = "/api/autorizacao/perfis";
    private protected const string Consultar = "teste:inscricoes:operar";
    private protected const string Sensivel = "teste:atendimento:analisar";
    private protected const string Protocolo = "23479.000123/2026-11";

    private protected VersoesDoPerfilNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private protected static object Dados(string nome, params string[] permissoes) =>
        new { nome, descricao = "Perfil de teste do ciclo da versão.", permissoes, protocolo = Protocolo, tamanhoDoDocumento = Pdf.Length };

    private protected static string Nome() => $"Perfil {Guid.NewGuid():N}";

    private protected static Envio LerEnvio(JsonElement corpo) => new(
        corpo.GetProperty("perfilId").GetGuid(),
        corpo.GetProperty("versaoId").GetGuid(),
        corpo.GetProperty("documentoId").GetGuid(),
        corpo.GetProperty("urlDeEnvio").GetString()!);

    private protected Task<HttpClient> GestorAsync() => ClienteComPermissoesAsync(Diogo, AprovarFuncional);

    private protected static async Task<Envio> CriarPerfilAsync(HttpClient gestor, params string[] permissoes)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, Dados(Nome(), permissoes));

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private protected static async Task<Envio> CriarVersaoAsync(HttpClient gestor, Guid perfilId, string nome, params string[] permissoes)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes", Dados(nome, permissoes));

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private protected static async Task<Envio> ReemitirAsync(HttpClient gestor, Guid perfilId, Guid versaoId)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes/{versaoId}/documento/envio");

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private protected static async Task ConfirmarEnvioAsync(HttpClient gestor, Envio envio)
    {
        string etag = await EnviarPdfAsync(new Uri(envio.UrlDeEnvio, UriKind.Absolute));
        using HttpResponseMessage confirmacao = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento/confirmacao", new { etag });
        (await CorpoAsync(confirmacao, HttpStatusCode.OK)).GetProperty("situacao").GetString().Should().Be("em_verificacao");
    }

    private protected async Task EnviarEVerificarAsync(HttpClient gestor, Envio envio)
    {
        await ConfirmarEnvioAsync(gestor, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(
            async () => await TemVigenteComVersaoAsync(gestor, envio.PerfilId, envio.VersaoId),
            $"a versão {envio.VersaoId} não entrou em vigor");
    }

    private protected static async Task<bool> TemVigenteAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor").ValueKind == JsonValueKind.Object;

    private protected static async Task<bool> TemVigenteComVersaoAsync(HttpClient gestor, Guid perfilId, Guid versaoId)
    {
        JsonElement composicao = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor");

        return composicao.ValueKind == JsonValueKind.Object && composicao.GetProperty("versaoId").GetGuid() == versaoId;
    }

    private protected static async Task<string?> SituacaoDoDocumentoAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("versaoPendente").GetProperty("situacaoDoDocumento").GetString();

    private protected static async Task<string?> RecusaDaUltimaVersaoAsync(HttpClient gestor, Guid perfilId)
    {
        JsonElement recusada = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("ultimaVersaoRecusada");

        return recusada.ValueKind == JsonValueKind.Object ? recusada.GetProperty("motivo").GetString() : null;
    }

    private protected static async Task<Guid[]> ParaPedirAsync(HttpClient client)
    {
        JsonElement pagina = await LerAsync(client, $"/api/autorizacao/perfis-para-pedido?limit=100");
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private protected async Task<string> SituacaoNoBancoAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.Database.SqlQuery<string>($"SELECT situacao AS \"Value\" FROM autorizacao.documentos_da_versao WHERE id = {documentoId}").SingleAsync();
    }

    private protected async Task<(bool Aberta, Guid VersaoId)[]> VigenciasAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        VigenciaDoPerfil[] vigencias = await db.VigenciasDosPerfis.AsNoTracking().Where(v => v.PerfilId == perfilId).ToArrayAsync();

        return [.. vigencias.Select(v => (v.Aberta, v.VersaoId))];
    }

    private protected async Task<int> LeiturasAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>()
            .LeiturasDosDocumentosDasVersoes.CountAsync(l => l.DocumentoId == documentoId);
    }

    private protected async Task<DesfechoDoResultadoDaVersao> RegistrarDiretoAsync(
        Guid documentoId, VeredictoDaVerificacao veredicto = VeredictoDaVerificacao.Verificado)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ResultadoDoDocumentoDaVersao>()
            .RegistrarAsync(ResultadoDe(documentoId, veredicto), CancellationToken.None);
    }

    private protected static ResultadoDaVerificacaoDeArquivo ResultadoDe(Guid documentoId, VeredictoDaVerificacao veredicto)
    {
        bool verificado = veredicto == VeredictoDaVerificacao.Verificado;

        return new(
            documentoId,
            veredicto,
            verificado ? MotivoDaRecusa.NaoSeAplica : MotivoDaRecusa.PdfComSenha,
            verificado ? new string('a', 64) : null,
            verificado ? Pdf.Length : null,
            verificado ? FormatoDoArquivo.Pdf : null);
    }

    private protected async Task<string> SituacaoDaVersaoNoBancoAsync(Guid versaoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.Database.SqlQuery<string>($"SELECT situacao AS \"Value\" FROM autorizacao.versoes_do_perfil WHERE id = {versaoId}").SingleAsync();
    }

    /// <summary>Concede o perfil na transação do chamador, pela mesma leitura travada da concessão real, e devolve o perfil avaliado.</summary>
    private protected static async Task<Perfil> ConcederAsync(IServiceProvider servicos, Persona beneficiario, Guid perfilId, EscopoDaConcessao escopo)
    {
        IConcessaoRepository repositorio = servicos.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Concessao concessao = Concessao.Conceder(
                IdentidadeDe(beneficiario),
                perfil,
                escopo,
                new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
                IdentidadeDe(Diogo),
                DateTimeOffset.UtcNow,
                servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Value!;
        await repositorio.AdicionarAsync(concessao);
        await servicos.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();

        return perfil;
    }

    private protected sealed record Envio(Guid PerfilId, Guid VersaoId, Guid DocumentoId, string UrlDeEnvio);
}
