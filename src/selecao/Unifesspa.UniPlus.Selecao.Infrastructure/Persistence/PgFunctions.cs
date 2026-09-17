namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Assinaturas que o EF Core mapeia para funções do banco. Nunca são executadas
/// em C#: existem para que a consulta LINQ possa chamá-las e o tradutor emitir a
/// função correspondente no SQL.
/// </summary>
public static class PgFunctions
{
    /// <summary>
    /// Mapeia <c>selecao.normalizar_para_comparacao(text)</c>: o texto sem
    /// acento e em minúsculas, que é a forma pela qual as listagens comparam e
    /// ordenam.
    /// </summary>
    /// <remarks>
    /// Existir como função do banco é o que permite a mesma regra valer na coluna
    /// gerada e numa comparação avulsa — sem ela, o título seria comparado pela forma
    /// normalizada e o número do edital pela forma crua, e as duas pontas da busca
    /// discordariam sobre o que é o mesmo texto.
    /// </remarks>
    public static string NormalizeForComparison(string? texto) =>
        throw new InvalidOperationException(
            "PgFunctions.NormalizeForComparison é um stub de EF Core e não pode ser chamado diretamente.");
}
