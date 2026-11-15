namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que se confere nos formulários que o processo declara (ADR-0171): o código é único no processo, e
/// a fase a que o formulário se liga está no cronograma. As recusas se acumulam (ADR-0125). O
/// formulário ainda sem fase não é recusado aqui: a publicação é que exige a fase.
/// </summary>
public static class ConferenciaDosFormulariosDeclarados
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyList<FormularioDeclarado> formularios, IEnumerable<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(cronograma);

        return [.. RecusasDeCodigoDuplicado(formularios), .. RecusasDeFaseForaDoCronograma(formularios, cronograma)];
    }

    private static IEnumerable<FieldError> RecusasDeCodigoDuplicado(IReadOnlyList<FormularioDeclarado> formularios)
    {
        HashSet<CodigoDoFormulario> jaDeclarados = [];
        for (int indice = 0; indice < formularios.Count; indice++)
        {
            if (!jaDeclarados.Add(formularios[indice].Codigo))
            {
                yield return new FieldError($"formularios[{indice}].codigo", new DomainError(
                    FormulariosDeclaradosErrorCodes.CodigoDuplicado,
                    "O código do formulário já identifica outro formulário do processo."));
            }
        }
    }

    private static IEnumerable<FieldError> RecusasDeFaseForaDoCronograma(
        IReadOnlyList<FormularioDeclarado> formularios, IEnumerable<FaseCronograma> cronograma)
    {
        HashSet<Guid> fasesDoCronograma = [.. cronograma.Select(static fase => fase.Id)];
        for (int indice = 0; indice < formularios.Count; indice++)
        {
            if (formularios[indice].FaseId is { } faseId && !fasesDoCronograma.Contains(faseId))
            {
                yield return new FieldError($"formularios[{indice}].faseId", new DomainError(
                    FormularioProcessoErrorCodes.FaseForaDoCronograma,
                    "A fase do formulário não está no cronograma do processo."));
            }
        }
    }
}
