namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O arquivo que o administrador envia ao armazenamento privado por URL pré-assinada e que só vira
/// dado de negócio quando a confirmação lê o conteúdo, valida contra o formato declarado e sela a
/// cópia. É o que o modelo de documento e o documento a publicar têm em comum.
/// </summary>
public interface IArquivoEnviado
{
    Guid Id { get; }

    /// <summary>A chave do envio, alvo da URL pré-assinada de PUT.</summary>
    string ObjectKey { get; }

    /// <summary>A chave da cópia selada, definida na confirmação.</summary>
    string? ObjectKeyConfirmado { get; }

    /// <summary>O tipo de mídia que o arquivo declara e que a cópia selada leva.</summary>
    string TipoDeMidia { get; }

    Result ValidarConteudo(long tamanhoBytes, string contentType, byte[] conteudo);

    Result Confirmar(long tamanhoBytes, string hashSha256, TimeProvider clock);
}
