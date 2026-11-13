namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa reaproveita a linha viva de mesmo código ao redefinir ou repor as bancas, para não
/// remover e inserir o mesmo código na mesma transação. O que a linha viva guarda além da origem
/// do tipo, o nome e a regra de deliberação, precisa acompanhar a banca declarada ou congelada.
/// </summary>
public static class AlinhamentoDasBancasDaEtapa
{
    /// <summary>Define as bancas da etapa e alinha às declaradas as linhas que ela reaproveitou.</summary>
    public static Result Definir(EtapaProcesso etapa, IReadOnlyList<BancaDaEtapa> declaradas)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(declaradas);

        Result definidas = etapa.DefinirBancas(declaradas);
        if (definidas.IsSuccess)
        {
            Alinhar(etapa.Bancas, declaradas);
        }

        return definidas;
    }

    internal static void Alinhar(IEnumerable<BancaDaEtapa> vivas, IEnumerable<BancaDaEtapa> declaradas)
    {
        List<BancaDaEtapa> disponiveis = [.. vivas];
        foreach (BancaDaEtapa declarada in declaradas)
        {
            disponiveis.Find(viva => MesmoCodigo(viva, declarada))?.Redeclarar(declarada);
        }
    }

    private static bool MesmoCodigo(BancaDaEtapa viva, BancaDaEtapa declarada) =>
        string.Equals(
            HashCanonicalComputer.NormalizeNfc(viva.Codigo),
            HashCanonicalComputer.NormalizeNfc(declarada.Codigo),
            StringComparison.Ordinal);
}
