namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Tracking;

/// <summary>Chamadas, leituras e acessos ao banco comuns às suítes da versão do perfil pelo host real.</summary>
public abstract class VersoesDoPerfilNoHostTestBase : PedidosDeConcessaoTestBase
{
    private protected const string RotaPerfis = "/api/autorizacao/perfis";
    private protected const string Consultar = "teste:inscricoes:operar";
    private protected const string Sensivel = "teste:atendimento:analisar";
    private protected const string EtapaGestao = "gestao_de_acessos";
    private protected const string EtapaProtecao = "protecao_de_dados";
    private protected const string Protocolo = "23479.000123/2026-11";

    private TransacaoRetidaDeTeste? _travaDaFila;

    private protected VersoesDoPerfilNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private protected static object Dados(string nome, params string[] permissoes) =>
        new { nome, descricao = "Perfil de teste do ciclo da versão.", permissoes, protocolo = Protocolo, tamanhoDoDocumento = Pdf.Length };

    private protected static object DadosComRemocao(string nome, string? motivo, params string[] permissoes) =>
        new
        {
            nome,
            descricao = "Perfil de teste do ciclo da versão.",
            permissoes,
            protocolo = Protocolo,
            tamanhoDoDocumento = Pdf.Length,
            motivoDaRemocao = motivo,
            categoriaDaRemocao = "incidente",
        };

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

    /// <summary>
    /// Leva a versão à vigência pelo caminho real: quem criou confirma o envio, o veredicto chega pela fila, a gestão
    /// aprova e, quando a versão acrescenta permissão protegida, a proteção de dados aprova em seguida.
    /// </summary>
    private protected async Task EnviarEVerificarAsync(HttpClient criador, Envio envio)
    {
        await ConfirmarEnvioAsync(criador, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(
            async () => await SituacaoDoDocumentoAsync(criador, envio.PerfilId) == "verificado",
            $"o documento da versão {envio.VersaoId} não foi verificado");
        await AprovarAteAVigenciaAsync(criador, envio);
    }

    /// <summary>Aprova a gestão e, se a versão ainda espera a proteção de dados, essa também; confere a vigência.</summary>
    private protected async Task AprovarAteAVigenciaAsync(HttpClient criador, Envio envio)
    {
        using HttpClient gestao = await GestaoAsync();
        JsonElement aposAGestao = await AprovarAsync(gestao, envio, EtapaGestao);
        if (aposAGestao.GetProperty("versaoPendente").ValueKind == JsonValueKind.Object)
        {
            using HttpClient protecao = await ProtecaoAsync();
            await AprovarAsync(protecao, envio, EtapaProtecao);
        }

        (await TemVigenteComVersaoAsync(criador, envio.PerfilId, envio.VersaoId)).Should().BeTrue();
    }

    private protected Task<HttpClient> GestaoAsync() => ClienteComPermissoesAsync(Isabella, AprovarFuncional);

    private protected Task<HttpClient> ProtecaoAsync() => ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

    /// <summary>Confere a recusa do perfil pelo status e pelo código público do catálogo de erros.</summary>
    private protected static async Task ConferirRecusaDoPerfilAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.perfil.{codigo}");
    }

    /// <summary>
    /// Dispara as chamadas na ordem dada contra o mesmo perfil travado pelo teste: cada uma só entra depois de a anterior
    /// ficar à espera da trava, então a fila do banco é a ordem declarada. Solta a trava e devolve as respostas na ordem.
    /// </summary>
    private protected async Task<HttpResponseMessage[]> EmFilaSobATravaAsync(Guid perfilId, params Func<Task<HttpResponseMessage>>[] chamadas)
    {
        await using TransacaoRetidaDeTeste trava = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);
        _travaDaFila = trava;
        await trava.Servicos.GetRequiredService<UniPlusDbContext>().Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM autorizacao.perfis WHERE id = {0} FOR UPDATE", perfilId);

        List<Task<HttpResponseMessage>> disparadas = await DispararEmFilaAsync(trava, chamadas);
        await trava.ConfirmarAsync();

        return await Task.WhenAll(disparadas);
    }

    private static async Task<List<Task<HttpResponseMessage>>> DispararEmFilaAsync(
        TransacaoRetidaDeTeste trava, Func<Task<HttpResponseMessage>>[] chamadas)
    {
        List<Task<HttpResponseMessage>> disparadas = [];
        foreach (Func<Task<HttpResponseMessage>> chamada in chamadas)
        {
            disparadas.Add(chamada());
            await trava.AguardarBloqueioAsync(disparadas.Count);
        }

        return disparadas;
    }

    /// <summary>Dentro de uma chamada de <see cref="EmFilaSobATravaAsync"/>: espera o número de sessões bloqueadas pela trava que o teste segura.</summary>
    private protected Task AguardarFilaDaTravaAsync(int sessoes) =>
        (_travaDaFila ?? throw new InvalidOperationException("Não há trava retida pelo teste.")).AguardarBloqueioAsync(sessoes);

    private protected static async Task<JsonElement> AprovarAsync(HttpClient cliente, Envio envio, string etapa)
    {
        using HttpResponseMessage resposta = await AprovarBrutoAsync(cliente, envio, etapa);

        return await CorpoAsync(resposta, HttpStatusCode.OK);
    }

    private protected static Task<HttpResponseMessage> AprovarBrutoAsync(HttpClient cliente, Envio envio, string etapa, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/aprovacao", new { etapa }, chave);

    private protected static Task<HttpResponseMessage> RecusarBrutoAsync(HttpClient cliente, Envio envio, string etapa, string? motivo = "Documento instituidor sem a designação.") =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/recusa", new { etapa, motivo });

    private protected static Task<HttpResponseMessage> CancelarBrutoAsync(HttpClient cliente, Envio envio, string? motivo = "Submetida por engano.", string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/cancelamento", new { motivo }, chave);

    private protected static Task<HttpResponseMessage> RemoverBrutoAsync(
        HttpClient cliente, Guid perfilId, string[] permissoes, string? motivo = "Permissão concedida por engano.", string? categoria = null, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/remocoes", new { permissoes, motivo, categoria }, chave);

    private protected static async Task<bool> TemVigenteAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor").ValueKind == JsonValueKind.Object;

    private protected static async Task<bool> TemVigenteComVersaoAsync(HttpClient gestor, Guid perfilId, Guid versaoId)
    {
        JsonElement composicao = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor");

        return composicao.ValueKind == JsonValueKind.Object && composicao.GetProperty("versaoId").GetGuid() == versaoId;
    }

    private protected static async Task<string?> SituacaoDoDocumentoAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("versaoPendente").GetProperty("situacaoDoDocumento").GetString();

    /// <summary>A situação da última versão que terminou sem entrar em vigor, ou nulo quando não há.</summary>
    private protected static async Task<string?> SituacaoDaUltimaEncerradaAsync(HttpClient gestor, Guid perfilId)
    {
        JsonElement encerrada = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("ultimaVersaoEncerradaSemVigor");

        return encerrada.ValueKind == JsonValueKind.Object ? encerrada.GetProperty("situacao").GetString() : null;
    }

    /// <summary>Todos os perfis que se podem pedir, percorrendo as páginas pelo cursor do cabeçalho Link.</summary>
    private protected static async Task<Guid[]> ParaPedirAsync(HttpClient client)
    {
        List<Guid> ids = [];
        Uri? proxima = new("/api/autorizacao/perfis-para-pedido?limit=100", UriKind.Relative);
        while (proxima is not null)
        {
            using HttpResponseMessage resposta = await client.GetAsync(proxima);
            string corpo = await resposta.Content.ReadAsStringAsync();
            resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
            JsonElement pagina = JsonDocument.Parse(corpo).RootElement;
            JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");
            ids.AddRange(itens.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
            proxima = resposta.Headers.TryGetValues("Link", out IEnumerable<string>? links) ? LinkDaProxima(links) : null;
        }

        return [.. ids];
    }

    private static Uri? LinkDaProxima(IEnumerable<string> links) =>
        links.SelectMany(static l => l.Split(',')).Select(static l => l.Trim())
            .Where(static l => l.EndsWith("rel=\"next\"", StringComparison.Ordinal))
            .Select(static l => new Uri(l[(l.IndexOf('<', StringComparison.Ordinal) + 1)..l.IndexOf('>', StringComparison.Ordinal)], UriKind.RelativeOrAbsolute))
            .FirstOrDefault();

    private protected async Task<string> SituacaoNoBancoAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQuery<string>($"SELECT situacao AS \"Value\" FROM autorizacao.documentos_da_versao WHERE id = {documentoId}").SingleAsync();
    }

    private protected async Task<(bool Aberta, Guid VersaoId)[]> VigenciasAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VigenciaDoPerfil[] vigencias = await db.VigenciasDosPerfis.AsNoTracking().Where(v => v.PerfilId == perfilId).ToArrayAsync();

        return [.. vigencias.Select(v => (v.Aberta, v.VersaoId))];
    }

    private protected async Task<int> LeiturasAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>()
            .LeiturasDosDocumentosDasVersoes.CountAsync(l => l.DocumentoId == documentoId);
    }

    private protected async Task<DesfechoDoResultadoDaVersao> RegistrarDiretoAsync(
        Guid documentoId, VeredictoDaVerificacao veredicto = VeredictoDaVerificacao.Verificado)
    {
        await using TransacaoRetidaDeTeste transacao = await TransacaoRetidaDeTeste.AbrirAsync(Fixture.Factory.Services);

        RegistroDoResultadoDaVersao registro = await transacao.Servicos.GetRequiredService<ResultadoDoDocumentoDaVersao>()
            .RegistrarAsync(ResultadoDe(documentoId, veredicto), CancellationToken.None);
        await transacao.ConfirmarAsync();

        return registro.Desfecho;
    }

    /// <summary>Entrega o veredicto pela fila do Wolverine, como o verificador faria, e espera o processamento terminar.</summary>
    /// <param name="aceitarFalha">Quando verdadeiro, a falha do processamento não derruba a espera: o teste a confere pelo estado.</param>
    private protected Task<ITrackedSession> EntregarPelaFilaAsync(
        Guid documentoId, VeredictoDaVerificacao veredicto = VeredictoDaVerificacao.Verificado, bool aceitarFalha = false)
    {
        TrackedSessionConfiguration sessao = Fixture.Factory.Services.GetRequiredService<IHost>()
            .TrackActivity()
            .IncludeExternalTransports()
            .Timeout(TimeSpan.FromSeconds(60));

        return (aceitarFalha ? sessao.DoNotAssertOnExceptionsDetected() : sessao)
            .ExecuteAndWaitAsync(bus => bus.EndpointFor(FilasDaVerificacao.ResultadosAutorizacao).SendAsync(ResultadoDe(documentoId, veredicto)));
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
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQuery<string>($"SELECT situacao AS \"Value\" FROM autorizacao.versoes_do_perfil WHERE id = {versaoId}").SingleAsync();
    }

    /// <summary>Compara os ids como o banco os ordena, que é a ordem em que o EF grava as linhas da mesma tabela.</summary>
    private protected async Task<bool> TemIdMenorAsync(Guid id, Guid outroId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQuery<bool>($"SELECT {id}::uuid < {outroId}::uuid AS \"Value\"").SingleAsync();
    }

    private protected async Task<int> QuantidadeDeVersoesVigentesAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM autorizacao.versoes_do_perfil WHERE perfil_id = {perfilId} AND situacao = 'vigente'").SingleAsync();
    }

    /// <summary>Concede o perfil na transação do chamador, pela mesma leitura travada da concessão real, e devolve o perfil avaliado.</summary>
    private protected static async Task<Perfil> ConcederAsync(IServiceProvider servicos, Persona beneficiario, Guid perfilId, EscopoDaConcessao escopo)
    {
        IConcessaoRepository repositorio = servicos.GetRequiredService<IConcessaoRepository>();
        Perfil perfil = (await repositorio.ObterParaConcederAsync(perfilId))!;
        Concessao concessao = ConcessaoBuilder.DoPerfil(perfil, servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>())
            .Para(IdentidadeDe(beneficiario))
            .NoEscopo(escopo)
            .ConcedidaPor(IdentidadeDe(Diogo))
            .Em(DateTimeOffset.UtcNow)
            .Construir();
        await repositorio.AdicionarAsync(concessao);
        await servicos.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();

        return perfil;
    }

    private protected sealed record Envio(Guid PerfilId, Guid VersaoId, Guid DocumentoId, string UrlDeEnvio);
}
