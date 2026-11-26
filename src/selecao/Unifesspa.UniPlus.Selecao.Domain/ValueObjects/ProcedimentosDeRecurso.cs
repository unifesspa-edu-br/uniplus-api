namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os procedimentos de recurso que uma fase ou etapa declara, em ordem canônica: tipo de decisão, tipo de alvo
/// (sem alvo primeiro), valor do alvo. A ordem não depende da de entrada, para que a mesma declaração dê o mesmo
/// envelope e o mesmo hash.
/// </summary>
/// <remarks>
/// Pressupõe a declaração já conferida por <see cref="ConferenciaDosProcedimentosDeRecurso"/>: não há duas
/// declarações da mesma matéria nem tipo coberto por declaração sem alvo e por declaração com alvo ao mesmo tempo.
/// </remarks>
public sealed class ProcedimentosDeRecurso
{
    public static ProcedimentosDeRecurso Nenhum { get; } = new([]);

    private ProcedimentosDeRecurso(IReadOnlyList<ProcedimentoDeRecursoDeclarado> itens) => Itens = itens;

    public IReadOnlyList<ProcedimentoDeRecursoDeclarado> Itens { get; }

    public static ProcedimentosDeRecurso De(IEnumerable<ProcedimentoDeRecursoDeclarado> declarados)
    {
        ArgumentNullException.ThrowIfNull(declarados);

        return new([.. NaOrdemDaConfiguracao(declarados)]);
    }

    /// <summary>A ordem canônica dos procedimentos, a mesma que a coleção guarda; quem liga cada um à posição em que veio no documento a usa.</summary>
    internal static IOrderedEnumerable<ProcedimentoDeRecursoDeclarado> NaOrdemDaConfiguracao(IEnumerable<ProcedimentoDeRecursoDeclarado> declarados) => declarados
        .OrderBy(static p => p.TipoDeDecisao.Codigo(), StringComparer.Ordinal)
        .ThenBy(static p => p.Alvo is null ? 0 : (int)p.Alvo.Tipo)
        .ThenBy(static p => p.Alvo?.Valor, StringComparer.Ordinal);

    /// <summary>Se as duas coleções declaram os mesmos procedimentos, comparados na ordem canônica.</summary>
    public bool MesmoConteudo(ProcedimentosDeRecurso outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        return Itens.Count == outra.Itens.Count
            && Itens.Zip(outra.Itens).All(static par => par.First.Equals(par.Second));
    }
}
