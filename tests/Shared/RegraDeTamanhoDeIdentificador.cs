namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text;

/// <summary>Um identificador de objeto de banco (<paramref name="Tipo"/>) que pertence à tabela <c>Esquema.Tabela</c>.</summary>
internal readonly record struct IdentificadorDoBanco(string Esquema, string Tabela, string Tipo, string Nome);

/// <summary>
/// O PostgreSQL guarda o identificador de qualquer objeto (tabela, coluna, chave, índice, restrição) com até
/// <see cref="LimiteDeBytes"/> bytes e corta o que passa disso, sem aviso: dois nomes longos com o mesmo começo viram o
/// mesmo identificador e o nome que sobra termina no meio de uma palavra. A regra só decide o tamanho; quem lê os
/// identificadores do modelo ou do catálogo é cada teste, sem lista de exceções por objeto.
/// </summary>
internal static class RegraDeTamanhoDeIdentificador
{
    public const int LimiteDeBytes = 63;

    /// <summary>Uma linha <c>esquema.tabela: tipo 'nome' ocupa N bytes...</c> para cada identificador acima do limite, em ordem.</summary>
    public static IReadOnlyList<string> Violacoes(IEnumerable<IdentificadorDoBanco> identificadores)
    {
        ArgumentNullException.ThrowIfNull(identificadores);

        return
        [
            .. identificadores
                .Where(i => Bytes(i.Nome) > LimiteDeBytes)
                .OrderBy(i => i.Esquema, StringComparer.Ordinal)
                .ThenBy(i => i.Tabela, StringComparer.Ordinal)
                .ThenBy(i => i.Tipo, StringComparer.Ordinal)
                .ThenBy(i => i.Nome, StringComparer.Ordinal)
                .Select(i => $"{i.Esquema}.{i.Tabela}: {i.Tipo} '{i.Nome}' ocupa {Bytes(i.Nome)} bytes e o limite é {LimiteDeBytes}"),
        ];
    }

    private static int Bytes(string nome) => Encoding.UTF8.GetByteCount(nome);
}
