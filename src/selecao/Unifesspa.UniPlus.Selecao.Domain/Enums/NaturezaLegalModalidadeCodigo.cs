namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="NaturezaLegalModalidade"/> e o token textual
/// (UPPER_SNAKE) exposto por <c>ModalidadeView.NaturezaLegal</c>
/// (Configuracao.Contracts, ADR-0056). Fonte única do parsing no snapshot-copy
/// (ADR-0061) — nunca comparar a string crua no handler.
/// </summary>
public static class NaturezaLegalModalidadeCodigo
{
    public const string CotaReservada = "COTA_RESERVADA";
    public const string Ampla = "AMPLA";
    public const string Suplementar = "SUPLEMENTAR";
    public const string OutraModalidade = "OUTRA_MODALIDADE";

    /// <summary>
    /// Converte o token cross-módulo para o enum local. Um token não
    /// reconhecido (contrato evoluiu no cadastro de origem antes deste
    /// módulo) mapeia para <see cref="NaturezaLegalModalidade.Nenhuma"/> — o
    /// sentinela que a factory de <see cref="Entities.ModalidadeSelecionada"/>
    /// já rejeita com um erro de domínio (422) claro, em vez de estourar uma
    /// exceção não tratada (500).
    /// </summary>
    public static NaturezaLegalModalidade FromCodigo(string? codigo) => codigo switch
    {
        CotaReservada => NaturezaLegalModalidade.CotaReservada,
        Ampla => NaturezaLegalModalidade.Ampla,
        Suplementar => NaturezaLegalModalidade.Suplementar,
        OutraModalidade => NaturezaLegalModalidade.OutraModalidade,
        _ => NaturezaLegalModalidade.Nenhuma,
    };
}
