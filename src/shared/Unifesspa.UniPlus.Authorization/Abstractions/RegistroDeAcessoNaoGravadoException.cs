namespace Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// O registro durável de acesso não pôde ser gravado. A borda a traduz em erro da requisição, e o dado que dependia do
/// registro não é entregue.
/// </summary>
public sealed class RegistroDeAcessoNaoGravadoException : Exception
{
    /// <summary>Cria a exceção com a causa da falha de gravação.</summary>
    public RegistroDeAcessoNaoGravadoException(Exception causa)
        : base("O registro de acesso não pôde ser gravado.", causa)
    {
    }

    /// <summary>Construtor sem causa, exigido pela convenção de exceções.</summary>
    public RegistroDeAcessoNaoGravadoException()
        : base("O registro de acesso não pôde ser gravado.")
    {
    }

    /// <summary>Construtor com mensagem, exigido pela convenção de exceções.</summary>
    public RegistroDeAcessoNaoGravadoException(string message)
        : base(message)
    {
    }

    /// <summary>Construtor com mensagem e causa, exigido pela convenção de exceções.</summary>
    public RegistroDeAcessoNaoGravadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
