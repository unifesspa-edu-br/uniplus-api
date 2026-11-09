namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O ciclo da versão do perfil pelo host real: o PDF sobe ao MinIO pela URL assinada, o veredicto volta pela fila e a
/// versão entra em vigor, ou é recusada, só então.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VersoesDoPerfilNoHostTests : PedidosDeConcessaoTestBase
{
    private const string RotaPerfis = "/api/autorizacao/perfis";
    private const string Consultar = "teste:inscricoes:operar";
    private const string Sensivel = "teste:atendimento:analisar";
    private const string Protocolo = "23479.000123/2026-11";

    public VersoesDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Perfil novo não é pedível até a vigência: criar, enviar, verificar, vigente e pedível")]
    public async Task Ciclo_CriarEnviarVerificar_TornaOPerfilPedivel()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        Guid unidade = await SemearUnidadeAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar);

        (await ParaPedirAsync(betina)).Should().NotContain(envio.PerfilId);
        using HttpResponseMessage antes = await CriarBrutoAsync(betina, envio.PerfilId, unidade, Leandro.Email);
        await ConferirRecusaAsync(antes, HttpStatusCode.UnprocessableEntity, "perfil_sem_versao_em_vigor");

        await EnviarEVerificarAsync(gestor, envio);

        (await ParaPedirAsync(betina)).Should().Contain(envio.PerfilId);
        using HttpResponseMessage depois = await CriarBrutoAsync(betina, envio.PerfilId, unidade, Leandro.Email);
        depois.StatusCode.Should().Be(HttpStatusCode.Created, await depois.Content.ReadAsStringAsync());
        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{envio.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.GetProperty("composicaoEmVigor").GetProperty("protocolo").GetString().Should().Be(Protocolo);
        perfil.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Cancelar a primeira versão e criar outra: a nova primeira versão pode mudar o alcance e entra em vigor")]
    public async Task Ciclo_CancelarEOutraPrimeiraVersao_EntraEmVigor()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio primeira = await CriarPerfilAsync(gestor, Consultar);
        (await EnviarAsync(gestor, HttpMethod.Delete, $"{RotaPerfis}/{primeira.PerfilId}/versoes/{primeira.VersaoId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Envio segunda = await CriarVersaoAsync(gestor, primeira.PerfilId, Nome(), "selecao:motivos-decisao-isencao:manter");
        await EnviarEVerificarAsync(gestor, segunda);

        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{primeira.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal("selecao:motivos-decisao-isencao:manter");
    }

    [Fact(DisplayName = "Documento recusado pela verificação libera outro documento para a mesma versão, que então entra em vigor")]
    public async Task Ciclo_DocumentoRecusado_PermiteOutroDocumento()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await ConfirmarEnvioAsync(gestor, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Recusado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(gestor, envio.PerfilId) == "recusado", "o documento não foi recusado");

        using HttpResponseMessage nova = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento", new { tamanhoDoDocumento = Pdf.Length });
        Envio outro = LerEnvio(await CorpoAsync(nova, HttpStatusCode.Created));
        await EnviarEVerificarAsync(gestor, outro);

        outro.DocumentoId.Should().NotBe(envio.DocumentoId);
        (await LerAsync(gestor, $"{RotaPerfis}/{envio.PerfilId}")).GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "Depois de perder a resposta, o versaoId lido do perfil recupera a URL de envio do mesmo documento")]
    public async Task Envio_RecuperaPeloVersaoIdLido()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio perdido = await CriarPerfilAsync(gestor, Consultar);
        JsonElement pendente = (await LerAsync(gestor, $"{RotaPerfis}/{perdido.PerfilId}")).GetProperty("versaoPendente");
        Guid versaoId = pendente.GetProperty("versaoId").GetGuid();

        Envio recuperado = await ReemitirAsync(gestor, perdido.PerfilId, versaoId);

        recuperado.DocumentoId.Should().Be(perdido.DocumentoId).And.Be(pendente.GetProperty("documentoId").GetGuid());
        await EnviarEVerificarAsync(gestor, recuperado);
    }

    [Fact(DisplayName = "Repetir a criação com a mesma chave devolve a mesma URL e não cria outro perfil")]
    public async Task Envio_ReplayDaCriacao_NaoDuplica()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        string chave = Guid.NewGuid().ToString();
        object corpo = Dados(Nome(), Consultar);

        using HttpResponseMessage primeira = await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, corpo, chave);
        using HttpResponseMessage repeticao = await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, corpo, chave);

        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        LerEnvio(await CorpoAsync(repeticao, HttpStatusCode.Created)).Should().Be(LerEnvio(await CorpoAsync(primeira, HttpStatusCode.Created)));
    }

    [Fact(DisplayName = "Veredictos tardios de versão cancelada só gravam o documento e não põem o perfil em vigor")]
    public async Task VeredictoTardio_VersaoCancelada_NaoEntraEmVigor()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio verificada = await CriarPerfilAsync(gestor, Consultar);
        Envio recusada = await CriarPerfilAsync(gestor, Consultar);
        foreach (Envio envio in new[] { verificada, recusada })
        {
            await ConfirmarEnvioAsync(gestor, envio);
            await EnviarAsync(gestor, HttpMethod.Delete, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}");
        }

        await InjetarResultadoAsync(verificada.DocumentoId, VeredictoDaVerificacao.Verificado);
        await InjetarResultadoAsync(recusada.DocumentoId, VeredictoDaVerificacao.Recusado);

        await AguardarAsync(async () => await SituacaoNoBancoAsync(verificada.DocumentoId) == "verificado", "veredicto tardio não gravado");
        await AguardarAsync(async () => await SituacaoNoBancoAsync(recusada.DocumentoId) == "recusado", "recusa tardia não gravada");
        foreach (Envio envio in new[] { verificada, recusada })
        {
            JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{envio.PerfilId}");
            perfil.GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Null);
            (await VigenciasAsync(envio.PerfilId)).Should().BeEmpty();
        }
    }

    [Fact(DisplayName = "O resultado repetido não abre outra vigência nem grava de novo")]
    public async Task VeredictoRepetido_NaoAbreOutraVigencia()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, envio);

        DesfechoDoResultadoDaVersao desfecho = await RegistrarDiretoAsync(envio.DocumentoId);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        (await VigenciasAsync(envio.PerfilId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "A troca de vigência encerra a anterior e abre a nova sob os índices parciais")]
    public async Task TrocaDeVigencia_FuncionaSobOsIndicesParciais()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio primeira = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, primeira);
        Envio segunda = await CriarVersaoAsync(gestor, primeira.PerfilId, Nome(), Consultar, Sensivel);

        await EnviarEVerificarAsync(gestor, segunda);

        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{primeira.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        (bool Aberta, Guid VersaoId)[] vigencias = await VigenciasAsync(primeira.PerfilId);
        vigencias.Should().HaveCount(2);
        vigencias.Count(v => v.Aberta).Should().Be(1);
        vigencias.Single(v => v.Aberta).VersaoId.Should().Be(segunda.VersaoId);
    }

    [Fact(DisplayName = "Nome tomado por outro perfil enquanto o documento esperava recusa a versão e a vigente segue")]
    public async Task CorridaDeNome_RecusaAVersao()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        string nomeNovo = Nome();
        Envio versao = await CriarVersaoAsync(gestor, original.PerfilId, nomeNovo, Consultar);
        await ConfirmarEnvioAsync(gestor, versao);
        (await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, Dados(nomeNovo, Consultar))).StatusCode.Should().Be(HttpStatusCode.Created);

        await InjetarResultadoAsync(versao.DocumentoId, VeredictoDaVerificacao.Verificado);

        await AguardarAsync(async () => await RecusaDaUltimaVersaoAsync(gestor, original.PerfilId) == "nome_indisponivel", "a versão não foi recusada");
        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{original.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
        Envio nova = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar);
        nova.VersaoId.Should().NotBe(versao.VersaoId);
    }

    [Fact(DisplayName = "Concessão surgida antes da entrada em vigor recusa a versão que amplia a sensibilidade")]
    public async Task ConcessaoAntesDaVigencia_RecusaAAmpliacao()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        Envio ampliada = await CriarVersaoAsync(gestor, original.PerfilId, Nome(), Consultar, Sensivel);
        await ConfirmarEnvioAsync(gestor, ampliada);
        await SemearConcessaoAsync(Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        await InjetarResultadoAsync(ampliada.DocumentoId, VeredictoDaVerificacao.Verificado);

        await AguardarAsync(
            async () => await RecusaDaUltimaVersaoAsync(gestor, original.PerfilId) == "ampliacao_exige_protecao_de_dados",
            "a ampliação não foi recusada");
        (await LerAsync(gestor, $"{RotaPerfis}/{original.PerfilId}")).GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "Concessão posterior à entrada em vigor recusa a próxima ampliação já na criação")]
    public async Task ConcessaoDepoisDaVigencia_RecusaANovaAmpliacao()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio original = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, original);
        await SemearConcessaoAsync(Leandro, original.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        using HttpResponseMessage resposta = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{original.PerfilId}/versoes", Dados(Nome(), Consultar, Sensivel));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("ampliacao_exige_protecao_de_dados");
    }

    [Fact(DisplayName = "Desativar antes do resultado: a pendente entra em vigor e o perfil segue desativado e não pedível")]
    public async Task Desativar_AntesDoResultado_EntraEmVigorSemFicarPedivel()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await ConfirmarEnvioAsync(gestor, envio);
        (await EnviarAsync(gestor, HttpMethod.Delete, $"{RotaPerfis}/{envio.PerfilId}/ativacao")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);

        await AguardarAsync(async () => await TemVigenteAsync(gestor, envio.PerfilId), "a pendente não entrou em vigor");
        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{envio.PerfilId}");
        perfil.GetProperty("ativo").GetBoolean().Should().BeFalse();
        (await ParaPedirAsync(betina)).Should().NotContain(envio.PerfilId);
    }

    [Fact(DisplayName = "Desativar depois do resultado tira o perfil da lista de pedíveis sem tirar a composição em vigor")]
    public async Task Desativar_DepoisDoResultado_SaiDaLista()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, envio);
        (await ParaPedirAsync(betina)).Should().Contain(envio.PerfilId);

        await EnviarAsync(gestor, HttpMethod.Delete, $"{RotaPerfis}/{envio.PerfilId}/ativacao");

        (await ParaPedirAsync(betina)).Should().NotContain(envio.PerfilId);
        (await TemVigenteAsync(gestor, envio.PerfilId)).Should().BeTrue();
    }

    [Fact(DisplayName = "A leitura do documento verificado grava a trilha e a de documento não verificado não grava")]
    public async Task Leitura_VerificadaGravaATrilha_NaoVerificadaNao()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio verificado = await CriarPerfilAsync(gestor, Consultar);
        await EnviarEVerificarAsync(gestor, verificado);
        Envio pendente = await CriarPerfilAsync(gestor, Consultar);
        string rota(Envio e) => $"{RotaPerfis}/{e.PerfilId}/versoes/{e.VersaoId}/documento";

        using HttpResponseMessage negada = await gestor.GetAsync(new Uri(rota(pendente), UriKind.Relative));
        using HttpResponseMessage lida = await gestor.GetAsync(new Uri(rota(verificado), UriKind.Relative));

        negada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        lida.StatusCode.Should().Be(HttpStatusCode.OK, await lida.Content.ReadAsStringAsync());
        lida.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await LeiturasAsync(verificado.DocumentoId)).Should().Be(1);
        (await LeiturasAsync(pendente.DocumentoId)).Should().Be(0);
    }

    private static object Dados(string nome, params string[] permissoes) =>
        new { nome, descricao = "Perfil de teste do ciclo da versão.", permissoes, protocolo = Protocolo, tamanhoDoDocumento = Pdf.Length };

    private static string Nome() => $"Perfil {Guid.NewGuid():N}";

    private static Envio LerEnvio(JsonElement corpo) => new(
        corpo.GetProperty("perfilId").GetGuid(),
        corpo.GetProperty("versaoId").GetGuid(),
        corpo.GetProperty("documentoId").GetGuid(),
        corpo.GetProperty("urlDeEnvio").GetString()!);

    private Task<HttpClient> GestorAsync() => ClienteComPermissoesAsync(Diogo, AprovarFuncional);

    private static async Task<Envio> CriarPerfilAsync(HttpClient gestor, params string[] permissoes)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, RotaPerfis, Dados(Nome(), permissoes));

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private static async Task<Envio> CriarVersaoAsync(HttpClient gestor, Guid perfilId, string nome, params string[] permissoes)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes", Dados(nome, permissoes));

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private static async Task<Envio> ReemitirAsync(HttpClient gestor, Guid perfilId, Guid versaoId)
    {
        using HttpResponseMessage resposta = await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes/{versaoId}/documento/envio");

        return LerEnvio(await CorpoAsync(resposta, HttpStatusCode.Created));
    }

    private static async Task ConfirmarEnvioAsync(HttpClient gestor, Envio envio)
    {
        string etag = await EnviarPdfAsync(new Uri(envio.UrlDeEnvio, UriKind.Absolute));
        using HttpResponseMessage confirmacao = await EnviarAsync(
            gestor, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento/confirmacao", new { etag });
        (await CorpoAsync(confirmacao, HttpStatusCode.OK)).GetProperty("situacao").GetString().Should().Be("em_verificacao");
    }

    private async Task EnviarEVerificarAsync(HttpClient gestor, Envio envio)
    {
        await ConfirmarEnvioAsync(gestor, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(
            async () => await TemVigenteComVersaoAsync(gestor, envio.PerfilId, envio.VersaoId),
            $"a versão {envio.VersaoId} não entrou em vigor");
    }

    private static async Task<bool> TemVigenteAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor").ValueKind == JsonValueKind.Object;

    private static async Task<bool> TemVigenteComVersaoAsync(HttpClient gestor, Guid perfilId, Guid versaoId)
    {
        JsonElement composicao = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("composicaoEmVigor");

        return composicao.ValueKind == JsonValueKind.Object && composicao.GetProperty("versaoId").GetGuid() == versaoId;
    }

    private static async Task<string?> SituacaoDoDocumentoAsync(HttpClient gestor, Guid perfilId) =>
        (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("versaoPendente").GetProperty("situacaoDoDocumento").GetString();

    private static async Task<string?> RecusaDaUltimaVersaoAsync(HttpClient gestor, Guid perfilId)
    {
        JsonElement recusada = (await LerAsync(gestor, $"{RotaPerfis}/{perfilId}")).GetProperty("ultimaVersaoRecusada");

        return recusada.ValueKind == JsonValueKind.Object ? recusada.GetProperty("motivo").GetString() : null;
    }

    private static async Task<Guid[]> ParaPedirAsync(HttpClient client)
    {
        JsonElement pagina = await LerAsync(client, $"/api/autorizacao/perfis-para-pedido?limit=100");
        JsonElement itens = pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items");

        return [.. itens.EnumerateArray().Select(i => i.GetProperty("id").GetGuid())];
    }

    private async Task<string> SituacaoNoBancoAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();

        return await db.Database.SqlQuery<string>($"SELECT situacao AS \"Value\" FROM autorizacao.documentos_da_versao WHERE id = {documentoId}").SingleAsync();
    }

    private async Task<(bool Aberta, Guid VersaoId)[]> VigenciasAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        VigenciaDoPerfil[] vigencias = await db.VigenciasDosPerfis.AsNoTracking().Where(v => v.PerfilId == perfilId).ToArrayAsync();

        return [.. vigencias.Select(v => (v.Aberta, v.VersaoId))];
    }

    private async Task<int> LeiturasAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>()
            .LeiturasDosDocumentosDasVersoes.CountAsync(l => l.DocumentoId == documentoId);
    }

    private async Task<DesfechoDoResultadoDaVersao> RegistrarDiretoAsync(Guid documentoId)
    {
        await using AsyncServiceScope scope = Fixture.Factory.Services.CreateAsyncScope();
        ResultadoDaVerificacaoDeArquivo resultado = new(
            documentoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, new string('a', 64), Pdf.Length, FormatoDoArquivo.Pdf);

        return await scope.ServiceProvider.GetRequiredService<ResultadoDoDocumentoDaVersao>().RegistrarAsync(resultado, CancellationToken.None);
    }

    private sealed record Envio(Guid PerfilId, Guid VersaoId, Guid DocumentoId, string UrlDeEnvio);
}
