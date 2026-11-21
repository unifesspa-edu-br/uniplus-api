namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O recurso que a fase ou a etapa admite, como valor: se admite instância superior e os procedimentos que declaram como cada
/// matéria é revisada, na ordem canônica. Cada procedimento é um item recorrido; o recurso sem procedimento tem um item só, sem
/// tipo de decisão.
/// </summary>
public sealed record RecursoDeclarado(bool AdmiteInstanciaSuperior, IReadOnlyList<ProcedimentoDeRecursoDeclarado> Procedimentos)
{
    internal static RecursoDeclarado Da(RegraRecursoFase regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        return new(regra.Parametros.AdmiteInstanciaSuperior, regra.Procedimentos.Itens);
    }

    internal static RecursoDeclarado Da(RecursoDaEtapa recurso)
    {
        ArgumentNullException.ThrowIfNull(recurso);

        return new(recurso.Parametros.AdmiteInstanciaSuperior, recurso.Procedimentos.Itens);
    }
}
