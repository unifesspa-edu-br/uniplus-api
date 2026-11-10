namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

/// <summary>As chamadas HTTP da suspensão, da retomada, da desativação e da reativação, comuns às suítes que as exercitam.</summary>
internal static class ChamadasDaContencao
{
    internal const string MotivoPadrao = "Uso indevido apontado pela gestão.";

    private const string RotaPerfis = "/api/autorizacao/perfis";

    internal static Task<HttpResponseMessage> SuspenderAsync(HttpClient cliente, Guid perfilId, string? motivo = MotivoPadrao, string? chave = null) =>
        PedidosDeConcessaoTestBase.EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/suspensao", new { motivo }, chave);

    internal static Task<HttpResponseMessage> RetomarAsync(HttpClient cliente, Guid perfilId, string? etapa, string? motivo = MotivoPadrao, string? chave = null) =>
        PedidosDeConcessaoTestBase.EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/suspensao/retomada", new { etapa, motivo }, chave);

    internal static Task<HttpResponseMessage> DesativarAsync(HttpClient cliente, Guid perfilId, string? motivo = MotivoPadrao) =>
        PedidosDeConcessaoTestBase.EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/desativacao", new { motivo });

    internal static Task<HttpResponseMessage> ReativarAsync(HttpClient cliente, Guid perfilId, string? motivo = MotivoPadrao) =>
        PedidosDeConcessaoTestBase.EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/reativacao", new { motivo });
}
