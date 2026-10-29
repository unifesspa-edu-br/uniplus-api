namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;

/// <summary>
/// O pedido de e-mail atravessa o outbox e a fila durável do host real e chega ao relay —
/// aqui, um Mailpit —, com as duas versões do corpo.
/// </summary>
[Collection(MonolitoComMailpitCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class EnvioDeEmailTests
{
    private const string Destinatario = "candidato@unifesspa.edu.br";

    private readonly MonolitoComMailpitFixture _fixture;

    public EnvioDeEmailTests(MonolitoComMailpitFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A mensagem chega ao destinatário com a versão em texto e a em HTML")]
    public async Task Publicar_ChegaAoMailpitComTextoEHtml()
    {
        string assunto = $"Código de instalação {Guid.NewGuid():N}";

        await PublicarAsync(assunto);

        JsonElement mensagem = await AguardarMensagemAsync(assunto, TimeSpan.FromSeconds(30));
        mensagem.GetProperty("To")[0].GetProperty("Address").GetString().Should().Be(Destinatario);
        mensagem.GetProperty("Text").GetString().Should().Contain("Seu código é 481516");
        mensagem.GetProperty("HTML").GetString().Should().Contain("<b>481516</b>");
    }

    [Fact(DisplayName = "Falha transitória do relay não perde a mensagem: ela chega na nova tentativa")]
    public async Task Publicar_RelayFalhaUmaVez_MensagemChegaDepois()
    {
        string assunto = $"Reenvio {Guid.NewGuid():N}";
        _fixture.Factory.FalhasPendentes = 1;

        // O que garante o "sem perda" é a fila durável: sem o roteamento, a mensagem iria para uma
        // fila local em memória e o teste de reenvio passaria sem provar o outbox.
        using (IServiceScope scope = _fixture.Factory.Services.CreateScope())
        {
            IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            bus.PreviewSubscriptions(NovaMensagem(assunto)).Select(e => e.Destination?.ToString())
                .Should().ContainSingle().Which.Should().Be("postgresql://email_envio/");
        }

        await PublicarAsync(assunto);

        // A primeira tentativa falha; o Wolverine retoma após o primeiro intervalo da política (1 s).
        JsonElement mensagem = await AguardarMensagemAsync(assunto, TimeSpan.FromSeconds(60));
        mensagem.GetProperty("To")[0].GetProperty("Address").GetString().Should().Be(Destinatario);
    }

    private async Task PublicarAsync(string assunto)
    {
        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.PublishAsync(NovaMensagem(assunto));
    }

    private static EnviarEmail NovaMensagem(string assunto) => EnviarEmail.Para(
        Email.Criar(Destinatario).Value!,
        assunto,
        "Seu código é 481516.",
        "<p>Seu código é <b>481516</b>.</p>",
        "instalacao.codigo");

    private async Task<JsonElement> AguardarMensagemAsync(string assunto, TimeSpan limite)
    {
        using HttpClient mailpit = new() { BaseAddress = _fixture.ApiDoMailpit };
        DateTime fim = DateTime.UtcNow + limite;

        while (DateTime.UtcNow < fim)
        {
            JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages");
            foreach (JsonElement resumo in lista.GetProperty("messages").EnumerateArray())
            {
                if (resumo.GetProperty("Subject").GetString() == assunto)
                {
                    string id = resumo.GetProperty("ID").GetString()!;
                    return await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"A mensagem '{assunto}' não chegou ao Mailpit em {limite}.");
    }
}
