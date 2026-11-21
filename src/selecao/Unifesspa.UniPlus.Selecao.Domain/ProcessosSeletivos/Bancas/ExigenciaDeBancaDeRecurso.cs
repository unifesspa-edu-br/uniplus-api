namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Todo recurso exige a banca de análise de recursos: a fase que admite recurso a declara, e a etapa
/// que abre janela de recurso a declara ou se serve da banca da fase em que acontece. Com as duas, o
/// recurso da etapa é julgado pela banca da etapa. A banca de recursos não tem recorte de competência.
/// </summary>
public static class ExigenciaDeBancaDeRecurso
{
    /// <summary>O código da recusa do recorte de competência na banca de recursos, para quem a projeta num item só.</summary>
    internal static readonly IReadOnlySet<string> CodigosDoRecorte = new HashSet<string>([BancaErrorCodes.RecorteEmBancaDeRecursos], StringComparer.Ordinal);

    /// <summary>A recusa do recorte de competência na banca de recursos declarada pela fase.</summary>
    public static DomainError? RecorteNaDeclaracao(string codigoDaFase, string tipoBancaCodigo, int categoriasNoRecorte) =>
        JulgamentoDasBancas.EhDeRecursos(tipoBancaCodigo) && categoriasNoRecorte > 0
            ? RecorteEmBancaDeRecursos($"da fase '{codigoDaFase}'")
            : null;

    private static DomainError RecorteEmBancaDeRecursos(string qual) =>
        new(BancaErrorCodes.RecorteEmBancaDeRecursos, $"A banca de análise de recursos {qual} não tem recorte de competência: ela julga todos os recursos da fase.");
}
