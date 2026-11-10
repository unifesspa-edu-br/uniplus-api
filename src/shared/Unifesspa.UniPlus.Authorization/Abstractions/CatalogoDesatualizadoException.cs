namespace Unifesspa.UniPlus.Authorization.Abstractions;

/// <summary>
/// O catálogo de permissões gravado no banco não é o desta instância: a instância é anterior à implantação que o trocou, ou
/// o banco ainda não foi conferido. A decisão de acesso falha fechada, e a borda a traduz em indisponibilidade
/// temporária.
/// </summary>
public sealed class CatalogoDesatualizadoException : Exception
{
    /// <summary>Cria a exceção com a mensagem padrão.</summary>
    public CatalogoDesatualizadoException()
        : base("O catálogo de permissões do banco não é o desta instância.")
    {
    }

    /// <summary>Construtor com mensagem, exigido pela convenção de exceções.</summary>
    public CatalogoDesatualizadoException(string message)
        : base(message)
    {
    }

    /// <summary>Construtor com mensagem e causa, exigido pela convenção de exceções.</summary>
    public CatalogoDesatualizadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
