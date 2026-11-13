namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que encerrou a concessão e quando, derivado na leitura pelo mesmo critério de <see cref="Concessao.EstaAtiva"/>: a
/// revogação, por qualquer caminho, ou o fim da vigência, que nunca é gravado.
/// </summary>
/// <param name="Causa">A revogação ou o vencimento.</param>
/// <param name="Em">O instante da revogação ou o fim da vigência.</param>
public sealed record EncerramentoDaConcessao(CausaDoEncerramento Causa, DateTimeOffset Em)
{
    /// <summary>O encerramento da concessão no instante; nulo enquanto ela está ativa.</summary>
    public static EncerramentoDaConcessao? De(Concessao concessao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return concessao switch
        {
            { RevogadaEm: { } revogadaEm } => new(CausaDoEncerramento.Revogada, revogadaEm),
            { ValidaAte: { } validaAte } when validaAte <= agora => new(CausaDoEncerramento.Vencida, validaAte),
            _ => null,
        };
    }
}
