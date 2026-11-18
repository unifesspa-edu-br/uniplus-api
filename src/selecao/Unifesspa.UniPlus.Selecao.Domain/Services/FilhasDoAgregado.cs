namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>A troca por inteiro de uma coleção de filhas do agregado, mantendo o vínculo com o pai.</summary>
internal static class FilhasDoAgregado
{
    // Materializa antes de limpar: as novas podem ser derivadas da própria coleção que se esvazia.
    internal static void Substituir<T>(List<T> atuais, IEnumerable<T> novas) => Substituir(atuais, novas, static _ => { });

    internal static void Substituir<T>(List<T> atuais, IEnumerable<T> novas, Action<T> vincularAoPai)
    {
        List<T> substitutas = [.. novas];
        atuais.Clear();
        foreach (T filha in substitutas)
        {
            vincularAoPai(filha);
            atuais.Add(filha);
        }
    }
}
