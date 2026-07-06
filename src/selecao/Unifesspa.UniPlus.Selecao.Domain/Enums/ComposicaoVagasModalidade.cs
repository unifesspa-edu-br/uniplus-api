namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Forma de composição aritmética da modalidade no total de vagas da oferta
/// (snapshot-copy do <c>ComposicaoVagas</c> de <c>Modalidade</c>, ADR-0061).
/// Determina como a modalidade entra na fórmula de conservação do
/// <c>QuadroDeVagas</c> (derivado, motor futuro):
/// <c>VR_final + RETIRADAS + AC = VO_base</c> e
/// <c>TotalPublicado = VO_base + Σ(SuplementarAoTotal)</c>.
/// </summary>
public enum ComposicaoVagasModalidade
{
    Nenhuma = 0,

    /// <summary>Ampla concorrência: o residual do VO_base após as reservas.</summary>
    ResidualDoVo = 1,

    /// <summary>Sub-reserva dentro do total reservado (VR) da Lei 12.711.</summary>
    DentroDoVr = 2,

    /// <summary>Retira vagas de outra modalidade (<see cref="ModalidadeSelecionada.ComposicaoOrigemCodigo"/>) — não soma ao total.</summary>
    RetiraDe = 3,

    /// <summary>Soma ao total publicado (<c>TotalPublicado = VO_base + n</c>) — vaga institucional adicional.</summary>
    SuplementarAoTotal = 4,
}
