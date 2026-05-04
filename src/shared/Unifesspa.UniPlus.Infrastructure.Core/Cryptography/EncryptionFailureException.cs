namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Lançada quando qualquer operação de criptografia ou decriptografia falha.
/// Não expõe detalhes internos (mensagem da causa) para não vazar informação sensível.
/// </summary>
public sealed class EncryptionFailureException : Exception
{
    public string KeyName { get; } = string.Empty;

    public EncryptionFailureException() { }

    public EncryptionFailureException(string message) : base(message) { }

    public EncryptionFailureException(string keyName, Exception inner)
        : base($"Falha na operação de criptografia para a chave '{keyName}'.", inner)
    {
        KeyName = keyName;
    }
}
