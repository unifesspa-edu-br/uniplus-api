namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Todo recurso exige a banca de análise de recursos: a fase que admite recurso a declara, e a etapa
/// que abre janela de recurso a declara ou se serve da banca da fase em que acontece. Com as duas, o
/// recurso da etapa é julgado pela banca da etapa. A banca de recursos atende todos os candidatos e não define candidatos atendidos.
/// </summary>
public static class ExigenciaDeBancaDeRecurso
{
    /// <summary>O código da recusa dos candidatos atendidos na banca de recursos, para quem a projeta num item só.</summary>
    internal static readonly IReadOnlySet<string> CodigosDosCandidatosAtendidos = new HashSet<string>([BancaErrorCodes.CandidatosAtendidosEmBancaDeRecursos], StringComparer.Ordinal);

    /// <summary>A recusa dos candidatos atendidos na banca de recursos declarada pela fase.</summary>
    public static DomainError? CandidatosAtendidosNaDeclaracao(string codigoDaFase, string tipoBancaCodigo, int categoriasNosCandidatosAtendidos) =>
        JulgamentoDasBancas.EhDeRecursos(tipoBancaCodigo) && categoriasNosCandidatosAtendidos > 0
            ? CandidatosAtendidosEmBancaDeRecursos($"da fase '{codigoDaFase}'")
            : null;

    private static DomainError CandidatosAtendidosEmBancaDeRecursos(string qual) =>
        new(BancaErrorCodes.CandidatosAtendidosEmBancaDeRecursos, $"A banca de análise de recursos {qual} define candidatos atendidos, mas a banca que julga recurso atende todos os candidatos. Retire as categorias de documento dela.");
}
