namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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

    internal static DomainError FaseSemBancaDeRecursos(string codigoDaFase) =>
        new(BancaErrorCodes.RecursoSemBanca, $"A fase '{codigoDaFase}' admite recurso e não declara a banca de análise de recursos.");

    internal static DomainError EtapaSemBancaDeRecursos(string nomeDaEtapa) =>
        new(
            BancaErrorCodes.RecursoSemBanca,
            $"A etapa '{Eco(nomeDaEtapa)}' abre recurso, e nem ela nem a fase em que acontece declaram a banca de análise de recursos.");

    /// <summary>A etapa que abre recurso e não tem quem o julgue: nem banca própria, nem banca de recursos na fase.</summary>
    internal static bool EtapaSemBancaQueJulgue(bool abreRecurso, bool temBancaPropria, bool faseTemBanca) =>
        abreRecurso && !temBancaPropria && !faseTemBanca;

    private static DomainError RecorteEmBancaDeRecursos(string qual) =>
        new(BancaErrorCodes.RecorteEmBancaDeRecursos, $"A banca de análise de recursos {qual} não tem recorte de competência: ela julga todos os recursos da fase.");

    private static string Eco(string nomeDaEtapa) =>
        CaracteresInvisiveis.ParaEco(nomeDaEtapa, TextoCongelado.TamanhoMaximoEcoado);
}
