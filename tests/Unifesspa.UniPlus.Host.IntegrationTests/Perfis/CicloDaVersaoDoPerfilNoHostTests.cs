namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>O ciclo da versão do perfil pelo host real: o PDF sobe ao MinIO pela URL assinada, o veredicto volta pela fila e a versão entra em vigor, ou é recusada, só então.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class CicloDaVersaoDoPerfilNoHostTests : VersoesDoPerfilNoHostTestBase
{
    private const string MotivoDaDesativacao = "Perfil fora de uso.";

    public CicloDaVersaoDoPerfilNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O perfil só pode ser pedido após a primeira versão entrar em vigor")]
    public async Task Ciclo_PrimeiraVersao_PermitePedirPerfil()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        using var cenario = new CenarioDaPrimeiraVersao(this, gestor, betina);

        await cenario.ExecutarAsync();

        cenario.PerfisAntes.Should().NotContain(cenario.Envio.PerfilId);
        await ConferirRecusaAsync(cenario.RespostaAntes, HttpStatusCode.UnprocessableEntity, "perfil_sem_versao_em_vigor");
        cenario.PerfisDepois.Should().Contain(cenario.Envio.PerfilId);
        cenario.RespostaDepois.StatusCode.Should().Be(HttpStatusCode.Created, await cenario.RespostaDepois.Content.ReadAsStringAsync());
        cenario.Perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        cenario.Perfil.GetProperty("composicaoEmVigor").GetProperty("protocolo").GetString().Should().Be(Protocolo);
        cenario.Perfil.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private sealed class CenarioDaPrimeiraVersao(CicloDaVersaoDoPerfilNoHostTests teste, HttpClient gestor, HttpClient betina) : IDisposable
    {
        public Envio Envio { get; private set; } = null!;
        public Guid[] PerfisAntes { get; private set; } = [];
        public Guid[] PerfisDepois { get; private set; } = [];
        public HttpResponseMessage RespostaAntes { get; private set; } = null!;
        public HttpResponseMessage RespostaDepois { get; private set; } = null!;
        public JsonElement Perfil { get; private set; }

        public void Dispose()
        {
            RespostaAntes?.Dispose();
            RespostaDepois?.Dispose();
        }

        public async Task ExecutarAsync()
        {
            Guid unidade = await teste.SemearUnidadeAsync();
            Envio = await CriarPerfilAsync(gestor, Consultar);
            PerfisAntes = await ParaPedirAsync(betina);
            RespostaAntes = await teste.CriarBrutoAsync(betina, Envio.PerfilId, unidade, Leandro.Email);
            await teste.EnviarEVerificarAsync(gestor, Envio);
            PerfisDepois = await ParaPedirAsync(betina);
            RespostaDepois = await teste.CriarBrutoAsync(betina, Envio.PerfilId, unidade, Leandro.Email);
            Perfil = await LerAsync(gestor, $"{RotaPerfis}/{Envio.PerfilId}");
        }
    }

    [Fact(DisplayName = "Cancelar a primeira versão e criar outra: a nova primeira versão pode mudar o alcance e entra em vigor")]
    public async Task Ciclo_CancelarEOutraPrimeiraVersao_EntraEmVigor()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio primeira = await CriarPerfilAsync(gestor, Consultar);
        using HttpResponseMessage cancelamento = await CancelarBrutoAsync(gestor, primeira);
        cancelamento.StatusCode.Should().Be(HttpStatusCode.OK, await cancelamento.Content.ReadAsStringAsync());

        Envio segunda = await CriarVersaoAsync(gestor, primeira.PerfilId, Nome(), "selecao:motivos-de-decisao:manter");
        await EnviarEVerificarAsync(gestor, segunda);

        JsonElement perfil = await LerAsync(gestor, $"{RotaPerfis}/{primeira.PerfilId}");
        perfil.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(2);
        perfil.GetProperty("composicaoEmVigor").GetProperty("permissoes").EnumerateArray()
            .Select(p => p.GetProperty("codigo").GetString()).Should().Equal("selecao:motivos-de-decisao:manter");
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
    public async Task Envio_RepeticaoDaCriacao_NaoDuplica()
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

    [Fact(DisplayName = "A desativação antes do resultado preserva a vigência sem permitir pedidos")]
    public async Task Desativar_AntesDoResultado_EntraEmVigorSemPoderSerPedido()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using HttpClient betina = Cliente(Betina);
        using var cenario = new CenarioDaDesativacao(this, gestor, betina);

        await cenario.ExecutarAsync();

        cenario.Resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cenario.Perfil.GetProperty("ativo").GetBoolean().Should().BeFalse();
        cenario.PerfisParaPedir.Should().NotContain(cenario.Envio.PerfilId);
    }

    private sealed class CenarioDaDesativacao(CicloDaVersaoDoPerfilNoHostTests teste, HttpClient gestor, HttpClient betina) : IDisposable
    {
        public Envio Envio { get; private set; } = null!;
        public HttpResponseMessage Resposta { get; private set; } = null!;
        public JsonElement Perfil { get; private set; }
        public Guid[] PerfisParaPedir { get; private set; } = [];

        public void Dispose() => Resposta?.Dispose();

        public async Task ExecutarAsync()
        {
            Envio = await CriarPerfilAsync(gestor, Consultar);
            await ConfirmarEnvioAsync(gestor, Envio);
            Resposta = await EnviarAsync(gestor, HttpMethod.Post, $"{RotaPerfis}/{Envio.PerfilId}/desativacao", new { motivo = MotivoDaDesativacao });
            await teste.VerificarDocumentoAsync(gestor, Envio);
            await teste.AprovarAteAVigenciaAsync(gestor, Envio);
            await AguardarAsync(async () => await TemVigenteAsync(gestor, Envio.PerfilId), "a pendente não entrou em vigor");
            Perfil = await LerAsync(gestor, $"{RotaPerfis}/{Envio.PerfilId}");
            PerfisParaPedir = await ParaPedirAsync(betina);
        }
    }

    [Fact(DisplayName = "Desativar depois do resultado tira o perfil da lista de perfis que podem ser pedidos sem tirar a composição em vigor")]
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

    [Fact(DisplayName = "A leitura de documento verificado grava a trilha e impede o cache")]
    public async Task LeituraVerificada_GravaTrilhaEImpedeCache()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        using var cenario = new CenarioDeLeituraVerificada(this, gestor);
        await cenario.ExecutarAsync();

        cenario.Resposta.StatusCode.Should().Be(HttpStatusCode.OK, await cenario.Resposta.Content.ReadAsStringAsync());
        cenario.Resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await LeiturasAsync(cenario.Envio.DocumentoId)).Should().Be(1);
    }

    private sealed class CenarioDeLeituraVerificada(CicloDaVersaoDoPerfilNoHostTests teste, HttpClient gestor) : IDisposable
    {
        public Envio Envio { get; private set; } = null!;
        public HttpResponseMessage Resposta { get; private set; } = null!;

        public async Task ExecutarAsync()
        {
            Envio = await CriarPerfilAsync(gestor, Consultar);
            await teste.EnviarEVerificarAsync(gestor, Envio);
            Resposta = await gestor.GetAsync(new Uri(RotaDoDocumento(Envio), UriKind.Relative));
        }

        public void Dispose() => Resposta?.Dispose();
    }

    [Fact(DisplayName = "A leitura de documento não verificado é recusada e não grava trilha")]
    public async Task LeituraNaoVerificada_RecusaENaoGravaTrilha()
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio pendente = await CriarPerfilAsync(gestor, Consultar);

        using HttpResponseMessage negada = await gestor.GetAsync(new Uri(RotaDoDocumento(pendente), UriKind.Relative));

        negada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LeiturasAsync(pendente.DocumentoId)).Should().Be(0);
    }

    private static string RotaDoDocumento(Envio envio) =>
        $"{RotaPerfis}/{envio.PerfilId}/versoes/{envio.VersaoId}/documento";
}
