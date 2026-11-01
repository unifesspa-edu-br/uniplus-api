namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// A regra de que, em cada módulo, um par (natureza, classificação) aponta para no máximo uma permissão de consulta
/// (ADR-0153). Par sem permissão significa que ninguém vê o item; par com duas tornaria a decisão ambígua.
/// </summary>
internal static class UnicidadeDoPar
{
    /// <summary>Um problema para cada par declarado por mais de uma permissão do mesmo módulo.</summary>
    /// <param name="definicoes">As definições, cada uma com o nome com que a mensagem a cita.</param>
    public static IEnumerable<string> Problemas(IEnumerable<(string Quem, DefinicaoDePermissao Definicao)> definicoes)
    {
        ArgumentNullException.ThrowIfNull(definicoes);

        return definicoes
            .Where(static d => d.Definicao.Natureza != NaturezaDoItem.Nenhuma)
            .GroupBy(static d => (d.Definicao.Modulo, d.Definicao.Natureza, d.Definicao.Requisito.Sensibilidade))
            .Where(static g => g.Count() > 1)
            .Select(static g => $"O par ({g.Key.Natureza}, {g.Key.Sensibilidade}) do módulo '{g.Key.Modulo}' aponta para mais de uma permissão: "
                + string.Join(", ", g.Select(static d => d.Quem).Order(StringComparer.Ordinal)) + ".");
    }
}
