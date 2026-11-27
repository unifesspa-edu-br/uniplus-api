namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using System.Globalization;

/// <summary>
/// Uma lista do conteúdo enviado e a posição que cada parte lida ocupava nela. O campo de uma recusa
/// que conta as partes lidas volta à posição que veio.
/// </summary>
/// <param name="Caminho">O caminho da lista, sem a posição.</param>
/// <param name="Posicoes">A posição de entrada de cada parte lida, na ordem em que foi lida.</param>
internal sealed record ListaDaEntrada(string Caminho, IReadOnlyList<int> Posicoes)
{
    /// <summary>
    /// O campo na posição de entrada, ou nulo quando não é de uma parte desta lista. O campo é o da parte
    /// inteira ou de dentro dela, depois de um ponto: a posição 1 não casa com a 10.
    /// </summary>
    public string? NaPosicaoDaEntrada(string campo)
    {
        if (PosicaoLida(campo) is not var (lida, fecha))
        {
            return null;
        }

        string dentro = campo[(fecha + 1)..];
        return dentro.Length == 0 || dentro[0] == '.' ? $"{Caminho}[{Posicoes[lida]}]{dentro}" : null;
    }

    /// <summary>A posição lida entre colchetes depois do caminho da lista e onde o colchete fecha.</summary>
    private (int Lida, int Fecha)? PosicaoLida(string campo)
    {
        int abre = Caminho.Length;
        if (!campo.StartsWith(Caminho, StringComparison.Ordinal) || campo.Length <= abre || campo[abre] != '[')
        {
            return null;
        }

        int fecha = campo.IndexOf(']', abre);
        return fecha > abre
            && int.TryParse(campo.AsSpan(abre + 1, fecha - abre - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int lida)
            && lida < Posicoes.Count
                ? (lida, fecha)
                : null;
    }
}
