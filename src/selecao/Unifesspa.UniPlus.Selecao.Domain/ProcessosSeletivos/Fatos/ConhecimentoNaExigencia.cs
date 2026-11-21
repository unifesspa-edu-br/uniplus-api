namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Se o fato é conhecido no formulário que entrega a exigência de documento: a situação da origem e do alcance das respostas
/// e, quando elas o permitem, a recusa de a fase em que ele fica conhecido ser posterior à da exigência.
/// </summary>
public sealed record ConhecimentoNaExigencia(ConhecimentoDoFatoNoPonto Situacao, DomainError? RecusaDaFase)
{
    /// <summary>Se o fato é conhecido no formulário da exigência, sem recusa pela fase em que ele fica conhecido.</summary>
    public bool Conhecido => Situacao == ConhecimentoDoFatoNoPonto.Conhecido && RecusaDaFase is null;
}
