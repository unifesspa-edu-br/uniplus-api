namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que cada tipo de motivo exige do cadastro e onde a decisão dele é avaliada (UNI-REQ-0283).
/// Uma implementação por tipo: a decisão nova entra por uma política nova, e não por um
/// <c>switch</c> espalhado pelo cadastro, pela escolha e pela cobertura.
/// </summary>
public interface IPoliticaDoTipoDeMotivo
{
    TipoMotivoDeDecisao Tipo { get; }

    /// <summary>
    /// Confere e normaliza a subclassificação declarada. O tipo que a define a exige; o que não
    /// a define a recusa, porque um valor sem significado no tipo viraria filtro que nunca casa.
    /// </summary>
    Result<string?> ConferirSubclassificacao(string? subclassificacao);

    /// <summary>Se o tipo admite motivo do <paramref name="sentido"/> informado.</summary>
    bool AdmiteSentido(SentidoDoMotivo sentido);

    /// <summary>
    /// Se a decisão do tipo é avaliada na <paramref name="fase"/>: é a janela dessa fase, e a das
    /// etapas que ela abriga, que marca o início da avaliação.
    /// </summary>
    bool AvaliaNaFase(FaseCronograma fase);
}
