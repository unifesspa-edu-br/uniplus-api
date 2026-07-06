namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Natureza jurídica da modalidade selecionada para a distribuição de vagas de
/// uma oferta (snapshot-copy do <c>NaturezaLegal</c> de <c>Modalidade</c>,
/// ADR-0061). Determina se a concorrência dupla (Lei 14.723/2023) se aplica —
/// obrigatória quando ao menos uma modalidade selecionada é
/// <see cref="CotaReservada"/> (INV-7 da modelagem P-A).
/// </summary>
public enum NaturezaLegalModalidade
{
    Nenhuma = 0,

    /// <summary>Cota da Lei 12.711/2012 (reservada) — dispara concorrência dupla.</summary>
    CotaReservada = 1,

    /// <summary>Ampla concorrência (o residual do VO_base após as reservas).</summary>
    Ampla = 2,

    /// <summary>Vaga suplementar institucional (soma ao total publicado, ex.: PSIQ).</summary>
    Suplementar = 3,

    /// <summary>Outra modalidade não coberta pela Lei 12.711 (ex.: PcD "V" retirada da AC).</summary>
    OutraModalidade = 4,
}
