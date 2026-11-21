namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// De onde o fato vem no processo: a fase em que o catálogo o situa, as fases dos formulários que o coletam e os fatos de que
/// o valor dele depende. É o que a pergunta "em que fase o fato fica conhecido" precisa saber, venha a configuração da
/// declaração do edital ou da cópia congelada com ele.
/// </summary>
internal interface IOrigensDoFato
{
    /// <summary>As fases do cronograma do processo.</summary>
    IReadOnlyCollection<FaseCronograma> Cronograma { get; }

    /// <summary>O código da fase em que o catálogo situa o fato, ou nulo quando não o situa.</summary>
    string? CodigoDaFaseDoCatalogo(string fato);

    /// <summary>As fases dos formulários que coletam o fato.</summary>
    IEnumerable<FaseCronograma> FasesDosFormulariosQueColetam(string fato);

    /// <summary>Os fatos de que o valor do fato depende: os que o derivam e, no agregado de grupo repetível, o fato de membro.</summary>
    IEnumerable<string> Dependencias(string fato);
}
