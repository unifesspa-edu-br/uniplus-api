namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A proteção do arquivo do candidato na confirmação: o antivírus olha o conteúdo ainda em claro na
/// memória, antes de qualquer gravação, e o conteúdo é selado cifrado. Implementada na
/// Infrastructure, que conhece o clamd e a chave de dado sensível; a Application só conhece a
/// porta.
/// </summary>
public interface IProtecaoDoConteudoSelado
{
    /// <summary>
    /// Verifica o conteúdo. Falha com <c>ArquivoInfectado</c> quando o antivírus aponta ameaça e com
    /// <c>AntivirusIndisponivel</c> quando não consegue verificar — nos dois casos nada é gravado e o
    /// arquivo continua pendente.
    /// </summary>
    Task<Result> VerificarAsync(byte[] conteudo, CancellationToken cancellationToken = default);

    /// <summary>Cifra o conteúdo em claro com a chave de dado sensível.</summary>
    Task<byte[]> SelarAsync(byte[] conteudo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decifra o conteúdo selado e confere o SHA-256 do conteúdo em claro contra o gravado na
    /// confirmação: o objeto trocado por outro registro, ou adulterado, não abre.
    /// </summary>
    Task<Result<byte[]>> AbrirAsync(byte[] selado, string hashSha256, CancellationToken cancellationToken = default);
}
