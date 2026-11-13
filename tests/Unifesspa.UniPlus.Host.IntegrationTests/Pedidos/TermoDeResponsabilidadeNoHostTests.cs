namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>A publicação e a leitura das versões do termo, e o pedido protegido sem termo publicado.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class TermoDeResponsabilidadeNoHostTests : TermoNoHostTestBase
{
    public TermoDeResponsabilidadeNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Publicação sem a permissão de publicar o termo é recusada com 403")]
    public async Task Publicar_SemPermissao_Recusa()
    {
        using HttpClient betina = Cliente(Betina);

        using HttpResponseMessage resposta = await PublicarAsync(betina, "sem permissão", Vigente!.Id);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ContarVersoesAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "O gestor publica a versão seguinte, e o resumo devolvido é o SHA-256 do texto devolvido")]
    public async Task Publicar_ResumoCorrespondeAoTexto()
    {
        using HttpClient diogo = await PublicadorAsync();

        JsonElement versao = await CorpoAsync(await PublicarAsync(diogo, "segunda versão", Vigente!.Id), HttpStatusCode.Created);

        versao.GetProperty("numero").GetInt32().Should().Be(2);
        versao.GetProperty("vigente").GetBoolean().Should().BeTrue();
        versao.GetProperty("resumo").GetString().Should().Be(Sha256(versao.GetProperty("texto").GetString()!));
        versao.TryGetProperty("autorSub", out _).Should().BeFalse("o autor fica gravado para auditoria e não é exposto");
    }

    [Fact(DisplayName = "Quem pede lê a vigente e a versão pelo id; o histórico é de quem publica")]
    public async Task Ler_VigenteVersaoEHistorico()
    {
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await PublicadorAsync();

        (await LerAsync(betina, $"{RotaDoTermo}/vigente")).GetProperty("id").GetGuid().Should().Be(Vigente!.Id);
        (await LerAsync(betina, $"{RotaDoTermo}/versoes/{Vigente.Id}")).GetProperty("numero").GetInt32().Should().Be(1);
        (await betina.GetAsync(new Uri($"{RotaDoTermo}/versoes", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonElement historico = await LerAsync(diogo, $"{RotaDoTermo}/versoes");
        (historico.ValueKind == JsonValueKind.Array ? historico : historico.GetProperty("items")).GetArrayLength().Should().Be(1);
        await ConferirAsync(await betina.GetAsync(new Uri($"{RotaDoTermo}/versoes/{Guid.NewGuid()}", UriKind.Relative)), HttpStatusCode.NotFound, "termo.versao_nao_encontrada");
    }

    [Fact(DisplayName = "Sem termo publicado, a vigente responde 404 e o pedido protegido, inclusive o de governança, responde 422")]
    public async Task SemTermo_RecusaOPedidoProtegido()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        await TermoNoHostDeTeste.ReiniciarAsync(Fixture.Factory.Services);
        using HttpClient betina = Cliente(Betina);

        await ConferirAsync(await betina.GetAsync(new Uri($"{RotaDoTermo}/vigente", UriKind.Relative)), HttpStatusCode.NotFound, "termo.sem_versao_vigente");
        await ConferirAsync(await PedirAsync(betina, perfil, unidade, null), HttpStatusCode.UnprocessableEntity, "pedido.sem_termo_vigente");
        await ConferirAsync(
            await EnviarAsync(betina, HttpMethod.Post, Rota, new { beneficiarioEmail = Leandro.Email, perfilId = Autorizacao.Domain.Entities.PerfisDeSistema.GestorDeAcessosId, justificativa = Justificativa }),
            HttpStatusCode.UnprocessableEntity,
            "pedido.sem_termo_vigente");
        (await ContarPedidosAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "O pedido não protegido não admite versão do termo, e o protegido para si exige a versão apresentada")]
    public async Task Criar_VersaoApresentadaIndevidaOuAusente()
    {
        Guid interno = await SemearPerfilInternoAsync();
        (Guid sensivel, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);

        await ConferirAsync(await PedirAsync(betina, interno, unidade, Vigente!.Id), HttpStatusCode.UnprocessableEntity, "pedido.apresentacao_do_termo_invalida");
        await ConferirAsync(await PedirAsync(betina, sensivel, unidade, null), HttpStatusCode.UnprocessableEntity, "pedido.apresentacao_do_termo_invalida");
        (await ContarPedidosAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "No pedido protegido para outra pessoa, a versão apresentada é obrigatória e conferida com a vigente")]
    public async Task ParaOutraPessoa_VersaoObrigatoriaEConferida()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        Guid antiga = Vigente!.Id;
        await TermoNoHostDeTeste.PublicarAsync(Fixture.Factory.Services, "segunda versão");

        await ConferirAsync(await PedirParaLeandroAsync(betina, perfil, unidade, null), HttpStatusCode.UnprocessableEntity, "pedido.apresentacao_do_termo_invalida");
        await ConferirAsync(await PedirParaLeandroAsync(betina, perfil, unidade, antiga), HttpStatusCode.Conflict, "pedido.termo_divergente");
        (await ContarPedidosAsync()).Should().Be(0);
    }

    private static Task<HttpResponseMessage> PedirParaLeandroAsync(HttpClient solicitante, Guid perfilId, Guid unidadeId, Guid? apresentada) =>
        EnviarAsync(
            solicitante, HttpMethod.Post, Rota,
            new { beneficiarioEmail = Leandro.Email, perfilId, unidadeId, justificativa = Justificativa, termoVersaoApresentadaId = apresentada });
}
