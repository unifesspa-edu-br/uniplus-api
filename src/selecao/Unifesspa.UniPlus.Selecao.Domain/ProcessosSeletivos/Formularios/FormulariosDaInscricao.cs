namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Os formulários que o candidato responde para se inscrever (ADR-0171): os obrigatórios ligados à fase
/// do cronograma que coleta a inscrição. O processo tem nenhum, um ou mais de um; a regra não limita a
/// quantidade, e a recusa da inscrição sem nenhum é de quem publica o processo.
/// </summary>
public static class FormulariosDaInscricao
{
    /// <summary>Os formulários da inscrição, em ordem ordinal do código.</summary>
    public static IReadOnlyList<FormularioDeclarado> De(
        IEnumerable<FormularioDeclarado> formularios, IEnumerable<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(cronograma);

        Guid[] fasesDeInscricao = [.. cronograma.Where(static fase => fase.ColetaInscricao).Select(static fase => fase.Id)];
        return [.. formularios
            .Where(formulario => formulario.Obrigatorio && formulario.FaseId is { } faseId && fasesDeInscricao.Contains(faseId))
            .OrderBy(static formulario => formulario.Codigo, CodigoDoFormulario.OrdemOrdinal)];
    }
}
