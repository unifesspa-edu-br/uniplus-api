namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O primeiro fato que fez a concessão deixar de valer, e quando, derivado na leitura pelo mesmo critério de
/// <see cref="Concessao.EstaAtiva"/>: a revogação, por qualquer caminho, ou o fim da vigência, que nunca é gravado.
/// </summary>
/// <param name="Causa">A revogação ou o vencimento.</param>
/// <param name="Em">O instante da revogação ou o fim da vigência.</param>
public sealed record EncerramentoDaConcessao(CausaDoEncerramento Causa, DateTimeOffset Em)
{
    /// <summary>
    /// O encerramento da concessão no instante; nulo enquanto ela está ativa. A revogação direta aceita a concessão já
    /// vencida, então o que encerrou é o que veio primeiro: o vencimento, se o fim da vigência chegou até o instante da
    /// revogação (no empate a concessão já não valia), senão a revogação.
    /// </summary>
    public static EncerramentoDaConcessao? De(Concessao concessao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        if (VencimentoDe(concessao, agora) is { } vencidaEm)
        {
            return new(CausaDoEncerramento.Vencida, vencidaEm);
        }

        return concessao.RevogadaEm is { } revogadaEm ? new(CausaDoEncerramento.Revogada, revogadaEm) : null;
    }

    /// <summary>
    /// O fim da vigência, quando a concessão venceu enquanto ainda não estava revogada: até o instante da revogação, se
    /// houve, ou até <paramref name="agora"/>. Nulo quando não venceu ou foi revogada antes de vencer.
    /// </summary>
    public static DateTimeOffset? VencimentoDe(Concessao concessao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return concessao.ValidaAte is { } fim && fim <= (concessao.RevogadaEm ?? agora) ? fim : null;
    }
}
