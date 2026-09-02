namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

using System;

/// <summary>
/// A origem respondeu com sucesso, mas o corpo não é o envelope que o contrato declara.
/// </summary>
/// <remarks>
/// Interrompe a varredura de propósito. O módulo existe para manter a réplica igual à
/// origem; se o envelope mudou, ninguém sabe mais o que a resposta significa, e seguir em
/// frente registraria como execução bem-sucedida uma leitura que não leu nada.
/// </remarks>
public sealed class EnvelopeDaOrigemInvalidoException : Exception
{
    public EnvelopeDaOrigemInvalidoException(string message)
        : base(message)
    {
    }

    public EnvelopeDaOrigemInvalidoException()
        : base("A resposta da origem não corresponde ao envelope declarado no contrato.")
    {
    }

    public EnvelopeDaOrigemInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
