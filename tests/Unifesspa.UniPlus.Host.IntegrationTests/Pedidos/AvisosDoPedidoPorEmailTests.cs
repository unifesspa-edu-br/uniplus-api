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
        await ResetarAsync();
        Guid perfil = await SemearPerfilInternoAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);

        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        JsonElement aceite = await AguardarAvisoAsync(Leandro, id, "aguarda o seu aceite");

        string link = $"{MonolitoApiFactory.EnderecoDaConfiguracao}/acessos/pedidos/{id}";
        aceite.GetProperty("Text").GetString().Should().Contain(link);
        aceite.GetProperty("HTML").GetString().Should().Contain($"href=\"{link}\"");
        SemDadosPessoais(aceite);
        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);

        SemDadosPessoais(await AguardarAvisoAsync(Diogo, id, "análise do gestor de acessos"));
        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        SemDadosPessoais(await AguardarAvisoAsync(Betina, id, "Pedido de acesso aprovado"));
        SemDadosPessoais(await AguardarAvisoAsync(Leandro, id, "Pedido de acesso aprovado"));
        LerLog().Should().NotContain(link).And.NotContain(Leandro.Email).And.NotContain(Betina.Email);
    }

    [Fact(DisplayName = "Pedido sensível avisa a proteção de dados sem quem aprovou como gestor, e a recusa avisa as partes sem o motivo")]
    public async Task PedidoSensivel_AvisaAProtecaoDeDadosForaDoGestorQueAprovou()
    {
        await ResetarAsync();
        Guid perfil = await SemearPerfilSensivelAsync();
        Guid unidade = await SemearUnidadeAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient isabella = await ClienteComPermissoesAsync(Isabella, AprovarFuncional, AprovarProtecaoDeDados);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);

        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);
        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite")).StatusCode.Should().Be(HttpStatusCode.OK);
        await AguardarAvisoAsync(Isabella, id, "análise do gestor de acessos");
        (await EnviarAsync(isabella, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        SemDadosPessoais(await AguardarAvisoAsync(Marlene, id, "análise da proteção de dados"));
        (await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/recusa", new { motivo = MotivoDaRecusa })).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement recusado = await AguardarAvisoAsync(Leandro, id, "Pedido de acesso recusado");
        await AguardarAvisoAsync(Betina, id, "Pedido de acesso recusado");
        recusado.GetProperty("Text").GetString().Should().NotContain(MotivoDaRecusa);
        (await MensagensAsync(Isabella, id)).Select(m => m.GetProperty("Subject").GetString()).Should()
            .NotContain(a => a!.Contains("proteção de dados", StringComparison.Ordinal), "quem aprovou como gestor não decide a segunda etapa");
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

    private static void SemDadosPessoais(JsonElement mensagem)
    {
        foreach (string texto in new[] { "Text", "HTML", "Subject" }.Select(campo => mensagem.GetProperty(campo).GetString()!))
        {
            texto.Should().NotContain(Betina.Nome).And.NotContain(Leandro.Nome).And.NotContain(Diogo.Nome)
                .And.NotContain(Justificativa).And.NotContain(Betina.Email).And.NotContain(Leandro.Email);
        }
    }

    private async Task<JsonElement> AguardarAvisoAsync(Persona destinatario, Guid pedidoId, string assunto)
    {
        DateTime fim = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < fim)
        {
            JsonElement[] mensagens = await MensagensAsync(destinatario, pedidoId);
            foreach (JsonElement mensagem in mensagens)
            {
                if (mensagem.GetProperty("Subject").GetString()!.Contains(assunto, StringComparison.OrdinalIgnoreCase)
                    || mensagem.GetProperty("Text").GetString()!.Contains(assunto, StringComparison.OrdinalIgnoreCase))
                {
                    return mensagem;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"O aviso \"{assunto}\" do pedido {pedidoId} não chegou a {destinatario.Username}.");
    }

    private async Task<JsonElement[]> MensagensAsync(Persona destinatario, Guid pedidoId)
    {
        using HttpClient mailpit = new() { BaseAddress = Fixture.ApiDoMailpit };
        JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages?limit=500");
        List<JsonElement> completas = [];
        foreach (JsonElement resumo in lista.GetProperty("messages").EnumerateArray())
        {
            if (!resumo.GetProperty("To").EnumerateArray().Any(t => string.Equals(
                t.GetProperty("Address").GetString(), destinatario.Email, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            JsonElement completa = await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{resumo.GetProperty("ID").GetString()}");
            if (completa.GetProperty("Text").GetString()!.Contains(pedidoId.ToString(), StringComparison.Ordinal))
            {
                completas.Add(completa);
            }
        }

        return [.. completas];
    }
}
