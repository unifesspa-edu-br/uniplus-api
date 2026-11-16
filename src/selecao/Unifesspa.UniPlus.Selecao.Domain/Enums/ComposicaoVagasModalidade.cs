namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Forma de composição aritmética da modalidade no total de vagas da oferta
/// (snapshot-copy do <c>ComposicaoVagas</c> de <c>Modalidade</c>, ADR-0061).
/// Determina como a modalidade entra na fórmula de conservação do
/// <c>QuadroDeVagas</c> (derivado, motor futuro):
/// <c>VR_final + RETIRADAS + AC = VO_base</c> e
/// <c>total de vagas do edital = vagas ofertadas base + Σ(SuplementarAoTotal)</c>.
/// </summary>
public enum ComposicaoVagasModalidade
{
    Nenhuma = 0,

    /// <summary>Ampla concorrência: o residual do VO_base após as reservas.</summary>
    RestanteDasVagasOfertadas = 1,

    /// <summary>Sub-reserva dentro do total reservado (VR) da Lei 12.711.</summary>
    DentroDaReservaLegal = 2,

    /// <summary>Retira vagas de outra modalidade (<see cref="ModalidadeSelecionada.ComposicaoOrigemCodigo"/>) — não soma ao total.</summary>
    RetiradaDeOutraModalidade = 3,

    /// <summary>Soma ao total de vagas do edital (<c>vagas ofertadas base + n</c>) — vaga institucional adicional.</summary>
    SuplementarAoTotal = 4,
}
