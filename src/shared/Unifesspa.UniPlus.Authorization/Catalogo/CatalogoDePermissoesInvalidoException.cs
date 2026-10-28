namespace Unifesspa.UniPlus.Authorization.Catalogo;

/// <summary>
/// O catálogo de permissões está inconsistente. Lançada na subida da aplicação,
/// com um item por defeito encontrado, cada um nomeando o membro responsável.
/// </summary>
public sealed class CatalogoDePermissoesInvalidoException : InvalidOperationException
{
    /// <summary>Cria a exceção a partir dos defeitos encontrados.</summary>
    public CatalogoDePermissoesInvalidoException(IReadOnlyList<string> problemas)
        : base(Montar(problemas))
    {
        Problemas = problemas;
    }

    /// <summary>Defeitos encontrados, um por item.</summary>
    public IReadOnlyList<string> Problemas { get; }

    private static string Montar(IReadOnlyList<string> problemas)
    {
        ArgumentNullException.ThrowIfNull(problemas);

        return "O catálogo de permissões é inválido:" + string.Concat(problemas.Select(static p => $"{Environment.NewLine} - {p}"));
    }
}
