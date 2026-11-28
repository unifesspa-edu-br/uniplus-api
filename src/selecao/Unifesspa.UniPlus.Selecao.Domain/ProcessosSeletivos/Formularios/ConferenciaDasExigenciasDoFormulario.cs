namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

/// <summary>
/// A exigência de documento declara o formulário que a entrega (ADR-0171). Ela só vale num formulário
/// que o processo tem e que é ligado à mesma fase da exigência, obrigatório ou facultativo: o pedido de
/// isenção, facultativo, exige o comprovante (UNI-REQ-0144). O formulário que ainda entrega alguma
/// exigência não pode ser removido. A gravação do rascunho aceita o formulário que ainda não existe (ADR-0171, emenda 3). As recusas independentes de uma mesma exigência saem juntas (ADR-0125).
/// </summary>
public static class ConferenciaDasExigenciasDoFormulario
{
    /// <summary>O campo do código do formulário, dentro do caminho da exigência.</summary>
    public const string CaminhoDoFormulario = "momento.formularioCodigo";

    /// <summary>
    /// As recusas de cada exigência, apontando o formulário pelo caminho dela em <paramref name="caminhosDasExigencias"/>
    /// (um por exigência, na mesma ordem). É a regra inteira: o formulário citado existe e é da fase da exigência.
    /// </summary>
    public static IReadOnlyList<FieldError> Conferir(
        IEnumerable<FormularioDeclarado> formularios, IReadOnlyList<ExigenciaDeDocumentoDoFormulario> exigencias, IReadOnlyList<string> caminhosDasExigencias) =>
        Recusas(formularios, exigencias, caminhosDasExigencias, RecusasDaExigencia);

    /// <summary>
    /// A parte da regra que a gravação do rascunho confere: o formulário que o processo tem precisa ser da fase da
    /// exigência. O que cita um formulário que ainda não existe é aceito, e a falta dele é recusada por <see cref="Conferir"/>
    /// na publicação e na retificação (ADR-0171, emenda 3).
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirConjunto(
        IEnumerable<FormularioDeclarado> formularios, IReadOnlyList<ExigenciaDeDocumentoDoFormulario> exigencias, IReadOnlyList<string> caminhosDasExigencias) =>
        Recusas(formularios, exigencias, caminhosDasExigencias, RecusasDaFase);

    private static IReadOnlyList<FieldError> Recusas(
        IEnumerable<FormularioDeclarado> formularios,
        IReadOnlyList<ExigenciaDeDocumentoDoFormulario> exigencias,
        IReadOnlyList<string> caminhosDasExigencias,
        Func<ExigenciaDeDocumentoDoFormulario, ILookup<CodigoDoFormulario, FormularioDeclarado>, IEnumerable<DomainError>> recusasDaExigencia)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(caminhosDasExigencias);
        CaminhosDasExigencias.ExigirUmParaCada(caminhosDasExigencias, exigencias.Count, nameof(caminhosDasExigencias));

        ILookup<CodigoDoFormulario, FormularioDeclarado> porCodigo = formularios.ToLookup(static formulario => formulario.Codigo);
        return [.. exigencias.SelectMany((exigencia, indice) => recusasDaExigencia(exigencia, porCodigo)
            .Select(recusa => new FieldError($"{caminhosDasExigencias[indice]}.{CaminhoDoFormulario}", recusa)))];
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
        formularios.Contains(exigencia.CodigoDoFormulario) ? RecusasDaFase(exigencia, formularios) : [FormularioInexistente()];

    private static IEnumerable<DomainError> RecusasDaFase(
        ExigenciaDeDocumentoDoFormulario exigencia, ILookup<CodigoDoFormulario, FormularioDeclarado> formularios) =>
        formularios[exigencia.CodigoDoFormulario].SelectMany(formulario => RecusasDoFormularioCitado(exigencia, formulario)).DistinctBy(static recusa => recusa.Code);

    private static IEnumerable<DomainError> RecusasDoFormularioCitado(ExigenciaDeDocumentoDoFormulario exigencia, FormularioDeclarado formulario)
    {
        if (exigencia.FaseId is { } faseDaExigencia && formulario.FaseId != faseDaExigencia)
        {
            yield return FormularioDeOutraFase();
        }
    }

    private static DomainError FormularioInexistente() => new(
        FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente,
        "O processo não tem o formulário citado pela exigência de documento.");

    private static DomainError FormularioDeOutraFase() => new(
        FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase,
        "O formulário citado é de outra fase que a da exigência de documento.");
}
