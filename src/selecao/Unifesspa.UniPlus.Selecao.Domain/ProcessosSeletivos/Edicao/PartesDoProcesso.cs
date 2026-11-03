namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Como as partes entram na coleção do processo: vinculadas a ele, na ordem em que chegaram.</summary>
internal static class PartesDoProcesso
{
    internal static void Substituir<T>(List<T> lista, IEnumerable<T> novas, Guid processoId)
        where T : class, IParteDoProcesso =>
        Substituir(lista, static _ => true, novas, processoId);

    internal static void Substituir<T>(List<T> lista, Predicate<T> substituidas, IEnumerable<T> novas, Guid processoId)
        where T : class, IParteDoProcesso
    {
        lista.RemoveAll(substituidas);
        foreach (T nova in novas)
        {
            nova.VincularProcesso(processoId);
            lista.Add(nova);
        }
    }

    internal static void Vincular(IParteDoProcesso parte, Guid processoId)
    {
        ArgumentNullException.ThrowIfNull(parte);

        parte.VincularProcesso(processoId);
    }
}
