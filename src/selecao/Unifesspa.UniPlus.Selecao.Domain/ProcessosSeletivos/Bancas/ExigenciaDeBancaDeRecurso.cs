namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Todo recurso exige a banca de análise de recursos: a fase que admite recurso a declara, e a etapa
/// que abre janela de recurso a declara ou se serve da banca da fase em que acontece. Com as duas, o
/// recurso da etapa é julgado pela banca da etapa. A banca de recursos não tem recorte de competência.
/// </summary>
public static class ExigenciaDeBancaDeRecurso
{
    internal static IEnumerable<DomainError> RecursosSemBanca(EstadoDaConfiguracao estado) =>
    [
        .. estado.CronogramaFases.Where(static fase => fase.RegraRecurso is not null && !JulgamentoDasBancas.TemBancaDeRecursos(fase))
            .Select(static fase => new DomainError(
                BancaErrorCodes.RecursoSemBanca,
                $"A fase '{fase.Codigo}' admite recurso e não declara a banca de análise de recursos.")),
        .. estado.Etapas.Where(etapa => EtapaSemBancaQueJulgueORecurso(estado, etapa))
            .Select(etapa => new DomainError(
                BancaErrorCodes.RecursoSemBanca,
                $"A etapa '{Eco(etapa)}' abre recurso, e nem ela nem a fase em que acontece declaram a banca de análise de recursos.")),
    ];

    internal static IEnumerable<DomainError> RecortesEmBancaDeRecursos(EstadoDaConfiguracao estado) =>
        estado.CronogramaFases
            .SelectMany(static fase => fase.BancasRequeridas.Select(banca => (fase, banca)))
            .Where(static par => JulgamentoDasBancas.EhDeRecursos(par.banca.Codigo) && par.banca.RecorteDeCompetencia.Count > 0)
            .Select(static par => RecorteEmBancaDeRecursos($"da fase '{par.fase.Codigo}'"));

    /// <summary>A recusa do recorte de competência na declaração da banca, antes de ela entrar na fase.</summary>
    public static DomainError? RecorteNaDeclaracao(string tipoBancaCodigo, int categoriasNoRecorte) =>
        JulgamentoDasBancas.EhDeRecursos(tipoBancaCodigo) && categoriasNoRecorte > 0
            ? RecorteEmBancaDeRecursos("declarada")
            : null;

    private static DomainError RecorteEmBancaDeRecursos(string qual) =>
        new(BancaErrorCodes.RecorteEmBancaDeRecursos, $"A banca de análise de recursos {qual} não tem recorte de competência: ela julga todos os recursos da fase.");

    private static bool EtapaSemBancaQueJulgueORecurso(EstadoDaConfiguracao estado, EtapaProcesso etapa) =>
        etapa.Recursos.Count > 0
        && !JulgamentoDasBancas.TemBancaDeRecursos(etapa)
        && !estado.CronogramaFases.Any(fase => string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal) && JulgamentoDasBancas.TemBancaDeRecursos(fase));

    private static string Eco(EtapaProcesso etapa) =>
        CaracteresInvisiveis.ParaEco(etapa.Nome, TextoCongelado.TamanhoMaximoEcoado);
}
