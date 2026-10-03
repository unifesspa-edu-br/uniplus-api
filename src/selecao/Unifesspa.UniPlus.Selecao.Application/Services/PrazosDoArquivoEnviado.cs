namespace Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>
/// Prazos das URLs pré-assinadas dos arquivos que o administrador envia ao processo — o documento
/// do Edital e o modelo de documento de uma exigência.
/// </summary>
public static class PrazosDoArquivoEnviado
{
    /// <summary>
    /// Validade da URL de envio, em segundos — curta por design. <c>const int</c> porque também é
    /// argumento de atributo: o cache de idempotência do início do envio expira no mesmo prazo,
    /// senão um replay devolveria uma URL já expirada.
    /// </summary>
    public const int EnvioSegundos = 900;

    /// <summary>
    /// Validade da URL de leitura, em segundos — bem menor que a de envio: cobre só o intervalo
    /// entre o clique e o navegador seguir o link, não a transferência de um arquivo grande.
    /// </summary>
    public const int LeituraSegundos = 300;

    public static readonly TimeSpan Envio = TimeSpan.FromSeconds(EnvioSegundos);

    public static readonly TimeSpan Leitura = TimeSpan.FromSeconds(LeituraSegundos);
}
