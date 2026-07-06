namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Caráter de uma etapa pontuada do Processo Seletivo. Etapas de caráter
/// <see cref="Classificatoria"/> ou <see cref="Ambas"/> compõem a nota final
/// e entram no divisor da média; etapas puramente <see cref="Eliminatoria"/>
/// (ex.: banca de heteroidentificação) aprovam/reprovam sem pontuar.
/// </summary>
public enum CaraterEtapa
{
    Nenhum = 0,
    Classificatoria = 1,
    Eliminatoria = 2,
    Ambas = 3
}
