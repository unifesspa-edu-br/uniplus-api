namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;

/// <summary>Configuração dos avisos por e-mail do pedido de concessão (<c>Autorizacao</c>).</summary>
public sealed class AvisosDoPedidoOptions
{
    public const string SectionName = "Autorizacao";

    /// <summary>
    /// Endereço absoluto (http ou https) da aplicação Configuração, sem barra final obrigatória; o link do e-mail
    /// acrescenta o caminho do pedido.
    /// </summary>
    public string EnderecoDaConfiguracao { get; init; } = string.Empty;

    public static bool EnderecoValido(string? endereco) =>
        Uri.TryCreate(endereco, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https";
}
