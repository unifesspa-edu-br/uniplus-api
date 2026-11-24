namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Caráter de uma etapa pontuada do Processo Seletivo. Etapas de caráter
/// <see cref="Classificatoria"/> ou <see cref="Ambas"/> compõem a nota final
/// e entram no divisor da média; etapas puramente <see cref="Eliminatoria"/>
/// (ex.: banca de heteroidentificação) aprovam/reprovam sem pontuar.
/// </summary>
public enum CaraterEtapa
{
    [CodigoDeContrato(
        "CLASSIFICATORIA",
        "Classificatória",
        Descricao = "A nota da etapa entra na nota final e na ordem de classificação dos candidatos.")]
    Classificatoria = 1,

    [CodigoDeContrato(
        "ELIMINATORIA",
        "Eliminatória",
        Descricao = "A etapa aprova ou reprova o candidato, sem pontuar.")]
    Eliminatoria = 2,

    [CodigoDeContrato(
        "AMBAS",
        "Classificatória e eliminatória",
        Descricao = "A nota da etapa entra na nota final e, além disso, pode eliminar o candidato.")]
    Ambas = 3,
}
