namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// Mede, em bytes UTF-8, o corpo JSON que o cliente envia, serializado com as opções JSON dos hosts. O texto acentuado vai
/// como o navegador o envia, em UTF-8 direto, e não como sequência de escape.
/// </summary>
public static class MedidorDoCorpoDoPedido
{
    private static readonly JsonSerializerOptions OpcoesDosHosts = LerOpcoesDosHosts();

    public static int Bytes<TCorpo>(TCorpo corpo) =>
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(corpo, OpcoesDosHosts));

    private static JsonSerializerOptions LerOpcoesDosHosts()
    {
        ServiceCollection servicos = new();
        servicos.AddMvcCore().Services.AddUniPlusJsonOptions();

        using ServiceProvider provedor = servicos.BuildServiceProvider();
        return new JsonSerializerOptions(provedor.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }
}
