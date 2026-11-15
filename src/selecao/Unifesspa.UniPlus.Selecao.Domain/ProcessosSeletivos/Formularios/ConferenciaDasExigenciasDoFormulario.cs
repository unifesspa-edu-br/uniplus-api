namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A exigência de documento declara o formulário que a entrega (ADR-0171). Ela só vale num formulário
/// que o processo tem, obrigatório, para que o candidato sempre o envie, e ligado à mesma fase da
/// exigência. O formulário que ainda entrega alguma exigência não pode ser removido. As recusas
/// independentes de uma mesma exigência saem juntas (ADR-0125).
/// </summary>
public static class ConferenciaDasExigenciasDoFormulario
{
    private const string CampoDaExigencia = "codigoDoFormulario";

    public static IReadOnlyList<FieldError> Conferir(
        IEnumerable<FormularioDeclarado> formularios, IReadOnlyList<ExigenciaDeDocumentoDoFormulario> exigencias)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(exigencias);

        ILookup<CodigoDoFormulario, FormularioDeclarado> porCodigo = formularios.ToLookup(static formulario => formulario.Codigo);
        return [.. exigencias.SelectMany((exigencia, indice) => RecusasDaExigencia(exigencia, porCodigo)
            .Select(recusa => new FieldError($"exigencias[{indice}].{CampoDaExigencia}", recusa)))];
    }

    /// <summary>Os formulários a remover que ainda entregam alguma exigência, uma recusa por formulário.</summary>
    public static IReadOnlyList<FieldError> ConferirRemocao(
        IReadOnlyList<CodigoDoFormulario> removidos, IEnumerable<ExigenciaDeDocumentoDoFormulario> exigencias)
    {
        ArgumentNullException.ThrowIfNull(removidos);
        ArgumentNullException.ThrowIfNull(exigencias);

        HashSet<CodigoDoFormulario> comExigencia = [.. exigencias.Select(static exigencia => exigencia.CodigoDoFormulario)];
        return [.. removidos
            .Select((codigo, indice) => (codigo, indice))
            .Where(removido => comExigencia.Contains(removido.codigo))
            .Select(static removido => new FieldError($"formulariosRemovidos[{removido.indice}]", new DomainError(
                FormulariosDeclaradosErrorCodes.RemocaoDeFormularioComExigencia,
                "O formulário entrega exigência de documento e não pode ser removido.")))];
    }

    private static IEnumerable<DomainError> RecusasDaExigencia(
        ExigenciaDeDocumentoDoFormulario exigencia, ILookup<CodigoDoFormulario, FormularioDeclarado> formularios) =>
        formularios.Contains(exigencia.CodigoDoFormulario)
            ? formularios[exigencia.CodigoDoFormulario].SelectMany(formulario => RecusasDoFormularioCitado(exigencia, formulario)).DistinctBy(static recusa => recusa.Code)
            : [FormularioInexistente()];

    private static IEnumerable<DomainError> RecusasDoFormularioCitado(ExigenciaDeDocumentoDoFormulario exigencia, FormularioDeclarado formulario)
    {
        if (!formulario.Obrigatorio)
        {
            yield return FormularioFacultativo();
        }

        if (formulario.FaseId != exigencia.FaseId)
        {
            yield return FormularioDeOutraFase();
        }
    }

    private static DomainError FormularioInexistente() => new(
        FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente,
        "O processo não tem o formulário citado pela exigência de documento.");

    private static DomainError FormularioFacultativo() => new(
        FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioFacultativo,
        "A exigência de documento precisa de formulário obrigatório, e o citado é facultativo.");

    private static DomainError FormularioDeOutraFase() => new(
        FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase,
        "O formulário citado é de outra fase que a da exigência de documento.");
}
