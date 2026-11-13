namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Os avisos por e-mail do pedido pelo host real, com o Mailpit como relay: cada transição entra no outbox junto da
/// gravação e chega a quem pode agir, com o link do pedido, sem nome de pessoa, justificativa nem motivo.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AvisosDoPedidoPorEmailTests : PedidosDeConcessaoTestBase
{
    private const string MotivoDaRecusa = "Motivo reservado da recusa.";

    public AvisosDoPedidoPorEmailTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Pedido para outra pessoa avisa o beneficiário, depois o gestor, e a aprovação avisa as duas partes, sem PII no corpo nem no log")]
    public async Task PedidoParaOutraPessoa_AvisaACadaTransicao()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: false);
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        string link = await ConferirAvisoDoAceiteAsync(id);
        await AceitoPorLeandroAsync(id);
        await AprovarDepoisDoAvisoAoGestorAsync(diogo, id);

        SemDadosPessoais(await AguardarAvisoAsync(Betina, id, "Pedido de acesso aprovado"));
        SemDadosPessoais(await AguardarAvisoAsync(Leandro, id, "Pedido de acesso aprovado"));
        LerLog().Should().NotContain(link).And.NotContain(Leandro.Email).And.NotContain(Betina.Email);
    }

    [Fact(DisplayName = "Pedido sensível avisa o Representante do controlador sem quem aprovou como gestor, e a recusa avisa as partes sem o motivo")]
    public async Task PedidoSensivel_AvisaAProtecaoDeDadosForaDoGestorQueAprovou()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional, AprovarProtecaoDeDados);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        Guid id = await PedidoDeBetinaAceitoAsync(perfil, unidade);
        await AprovarComoGestorAsync(isabella, id);

        SemDadosPessoais(await AguardarAvisoAsync(Marlene, id, "decisão do Representante do controlador"));
        (await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = MotivoDaRecusa })).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement recusado = await AguardarAvisoAsync(Leandro, id, "Pedido de acesso recusado");
        await AguardarAvisoAsync(Betina, id, "Pedido de acesso recusado");
        recusado.GetProperty("Text").GetString().Should().NotContain(MotivoDaRecusa);
        (await MensagensAsync(Isabella, id)).Select(m => m.GetProperty("Subject").GetString()).Should()
            .NotContain(a => a!.Contains("Representante do controlador", StringComparison.Ordinal), "quem aprovou como gestor não decide a segunda etapa");
    }

    [Fact(DisplayName = "O cancelamento avisa o beneficiário e não o solicitante")]
    public async Task Cancelamento_AvisaSoOBeneficiario()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);

        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await EnviarAsync(betina, HttpMethod.Delete, $"{Rota}/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AguardarAvisoAsync(Leandro, id, "Pedido de acesso cancelado");
        (await MensagensAsync(Betina, id)).Should().BeEmpty();
    }

    // O aviso do aceite leva ao pedido pelo link da tela, sem dado pessoal; devolve o link, que o log não pode trazer.
    private async Task<string> ConferirAvisoDoAceiteAsync(Guid id)
    {
        JsonElement aceite = await AguardarAvisoAsync(Leandro, id, "aguarda o seu aceite");
        string link = $"{MonolitoApiFactory.EnderecoDaConfiguracao}/acessos/pedidos/{id}";
        aceite.GetProperty("Text").GetString().Should().Contain(link);
        aceite.GetProperty("HTML").GetString().Should().Contain($"href=\"{link}\"");
        SemDadosPessoais(aceite);

        return link;
    }

    // O gestor tem a concessão antes do aceite: o aviso à fila do gestor sai para quem é gestor naquele instante.
    private async Task AprovarDepoisDoAvisoAoGestorAsync(HttpClient diogo, Guid id)
    {
        SemDadosPessoais(await AguardarAvisoAsync(Diogo, id, "análise do gestor de acessos"));
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task AprovarComoGestorAsync(HttpClient isabella, Guid id)
    {
        await AguardarAvisoAsync(Isabella, id, "análise do gestor de acessos");
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> PedidoDeBetinaAceitoAsync(Guid perfil, Guid unidade)
    {
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        await AceitoPorLeandroAsync(id);

        return id;
    }

    private async Task AceitoPorLeandroAsync(Guid id)
    {
        using HttpClient leandro = Cliente(Leandro);
        (await AceitarAsync(leandro, id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static void SemDadosPessoais(JsonElement mensagem)
    {
        foreach (string texto in new[] { "Text", "HTML", "Subject" }.Select(campo => mensagem.GetProperty(campo).GetString()!))
        {
            texto.Should().NotContain(Betina.Nome).And.NotContain(Leandro.Nome).And.NotContain(Diogo.Nome)
                .And.NotContain(Justificativa).And.NotContain(Betina.Email).And.NotContain(Leandro.Email);
        }
    }
}
