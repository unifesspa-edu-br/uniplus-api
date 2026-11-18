namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Quais respostas um ponto de uso alcança (ADR-0171): as de um formulário obrigatório respondido
/// antes dele, ou na mesma fase quando o ponto é uma exigência ou um formulário facultativo. A resposta
/// de um formulário facultativo não é fonte de ninguém, o que impede o ciclo; dois obrigatórios na
/// mesma fase não se alcançam. A fase que abre a solicitação de isenção conta como contemporânea da
/// inscrição quando a ordem a coloca antes; havendo mais de uma fase que coleta a inscrição, vale a de
/// menor ordem. Os códigos dos formulários são únicos no processo, o que a conferência dos formulários
/// declarados garante antes de o alcance ser consultado.
/// </summary>
public static class AlcanceDasRespostas
{
    /// <summary>Se quem usa, no ponto, pode usar a resposta coletada pelo produtor.</summary>
    public static bool Alcanca(PontoDeUso ponto, FormularioDeclarado produtor, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(ponto);
        ArgumentNullException.ThrowIfNull(produtor);
        ArgumentNullException.ThrowIfNull(cronograma);

        return ponto.Formulario?.Codigo == produtor.Codigo
            || (produtor.Obrigatorio
                && FaseDoProdutor(produtor, cronograma) is { } faseDoProdutor
                && JaRespondidaQuandoOPontoUsa(ponto, faseDoProdutor, cronograma));
    }

    /// <summary>Os formulários cujas respostas o ponto usa, em ordem ordinal do código do formulário.</summary>
    public static IReadOnlyList<FormularioDeclarado> Fontes(
        PontoDeUso ponto, IEnumerable<FormularioDeclarado> formularios, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        ArgumentNullException.ThrowIfNull(formularios);
        return [.. formularios
            .Where(formulario => Alcanca(ponto, formulario, cronograma))
            .OrderBy(static formulario => formulario.Codigo, CodigoDoFormulario.OrdemOrdinal)];
    }

    /// <summary>
    /// A resposta do obrigatório já existe quando o ponto a usa: o produtor vem numa fase anterior, ou
    /// na mesma quando o ponto é uma exigência ou um formulário facultativo.
    /// </summary>
    private static bool JaRespondidaQuandoOPontoUsa(PontoDeUso ponto, FaseCronograma faseDoProdutor, IEnumerable<FaseCronograma> cronograma)
    {
        int ordemDoProdutor = OrdemEfetivaDaFase(faseDoProdutor, cronograma);
        int ordemDoPonto = OrdemEfetivaDaFase(ponto.Fase, cronograma);
        return ordemDoProdutor < ordemDoPonto
            || (ordemDoProdutor == ordemDoPonto && ponto.Formulario is not { Obrigatorio: true });
    }

    private static FaseCronograma? FaseDoProdutor(FormularioDeclarado produtor, IEnumerable<FaseCronograma> cronograma) =>
        produtor.FaseId is { } faseId ? cronograma.FirstOrDefault(fase => fase.Id == faseId) : null;

    /// <summary>A ordem da fase; a da solicitação de isenção vale a da inscrição quando esta vem depois.</summary>
    private static int OrdemEfetivaDaFase(FaseCronograma fase, IEnumerable<FaseCronograma> cronograma) =>
        fase.ColetaSolicitacaoIsencao && OrdemDaPrimeiraFaseDeInscricao(cronograma) is { } ordemDaInscricao
            ? Math.Max(fase.Ordem, ordemDaInscricao)
            : fase.Ordem;

    private static int? OrdemDaPrimeiraFaseDeInscricao(IEnumerable<FaseCronograma> cronograma) =>
        cronograma.Where(static fase => fase.ColetaInscricao).Select(static fase => (int?)fase.Ordem).Min();
}
