namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A etapa reaproveita a linha viva de mesmo código ao redefinir ou reidratar as bancas, para não
/// remover e inserir o mesmo código na mesma transação; a linha reaproveitada assume a origem, o nome
/// e a regra de deliberação da banca declarada ou congelada.
/// </summary>
public static class AlinhamentoDasBancasDaEtapa
{
    /// <summary>
    /// Se duas das bancas têm o mesmo código. O código é comparado em NFC, como o documento canônico o
    /// emite: duas grafias do mesmo código passariam a guarda para depois sair do envelope como duas
    /// bancas de código idêntico.
    /// </summary>
    internal static bool CodigoRepetido(IEnumerable<BancaDaEtapa> bancas)
    {
        List<string> codigos = [.. bancas.Select(static b => HashCanonicalComputer.NormalizeNfc(b.Codigo))];

        return codigos.Distinct(StringComparer.Ordinal).Count() != codigos.Count;
    }

    internal static DomainError? BancaRepetida(IEnumerable<BancaDaEtapa> bancas) =>
        CodigoRepetido(bancas)
            ? new DomainError("EtapaProcesso.BancaDuplicadaNaEtapa", "Cada tipo de banca pode ser requerido uma única vez por etapa.")
            : null;

    /// <summary>
    /// As bancas que a etapa passa a ter: a linha viva de mesmo código é reaproveitada, assumindo a
    /// declaração, e a banca sem linha viva entra como chegou. O código identifica a banca, e a
    /// linha que continua declarada continua sendo a mesma linha, senão a mesma transação removeria e
    /// inseriria o mesmo código no índice único da etapa. Cada linha viva é consumida no máximo uma vez.
    /// </summary>
    /// <returns>As bancas que ficam, na ordem das declaradas ou congeladas.</returns>
    internal static List<BancaDaEtapa> Reconciliar(IEnumerable<BancaDaEtapa> vivas, IEnumerable<BancaDaEtapa> declaradas)
    {
        List<BancaDaEtapa> disponiveis = [.. vivas];

        return [.. declaradas.Select(declarada => ReaproveitarOuAdotar(disponiveis, declarada))];
    }

    private static BancaDaEtapa ReaproveitarOuAdotar(List<BancaDaEtapa> disponiveis, BancaDaEtapa declarada)
    {
        BancaDaEtapa? viva = disponiveis.Find(candidata => MesmoCodigo(candidata, declarada));
        if (viva is null)
        {
            return declarada;
        }

        disponiveis.Remove(viva);
        viva.AssumirDeclaracao(declarada);

        return viva;
    }

    private static bool MesmoCodigo(BancaDaEtapa viva, BancaDaEtapa declarada) =>
        string.Equals(
            HashCanonicalComputer.NormalizeNfc(viva.Codigo),
            HashCanonicalComputer.NormalizeNfc(declarada.Codigo),
            StringComparison.Ordinal);
}
