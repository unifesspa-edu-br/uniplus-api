namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>O ciclo da versão do perfil pelo host real: o PDF sobe ao MinIO pela URL assinada, o veredicto volta pela fila e a versão entra em vigor, ou é recusada, só então.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class CicloDaVersaoDoPerfilNoHostTests : VersoesDoPerfilNoHostTestBase
{
    private const string MotivoDaDesativacao = "Perfil fora de uso.";

    public CicloDaVersaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
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
        using HttpResponseMessage cancelamento = await CancelarBrutoAsync(gestor, primeira);
        cancelamento.StatusCode.Should().Be(HttpStatusCode.OK, await cancelamento.Content.ReadAsStringAsync());

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

    [Fact(DisplayName = "Desativar antes do resultado: a pendente entra em vigor e o perfil segue desativado e não pedível")]
    public async Task Desativar_AntesDoResultado_EntraEmVigorSemFicarPedivel()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await ConfirmarEnvioAsync(gestor, envio);
        (await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/desativacao", new { motivo = MotivoDaDesativacao })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(gestor, envio.PerfilId) == "verificado", "o documento não foi verificado");

        await AprovarAteAVigenciaAsync(gestor, envio);

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

        await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/desativacao", new { motivo = MotivoDaDesativacao });

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
}
