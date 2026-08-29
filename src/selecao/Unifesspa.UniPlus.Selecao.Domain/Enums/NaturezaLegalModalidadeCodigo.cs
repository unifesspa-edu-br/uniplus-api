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
    /// <summary>
    /// Converte o enum local de volta ao token cross-módulo — o mesmo que
    /// <c>GET /api/configuracao/modalidades</c> publica para a modalidade de
    /// origem. É o que a projeção de leitura emite, para que o cliente que
    /// cruza as duas fontes compare tokens iguais.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se <paramref name="natureza"/> é <see cref="NaturezaLegalModalidade.Nenhuma"/>:
    /// o sentinela não é natureza persistível — <see cref="Entities.ModalidadeSelecionada"/>
    /// recusa criá-la —, então encontrá-lo aqui denuncia corrupção, não um caso a projetar.
    /// </exception>
    public static string ToCodigo(this NaturezaLegalModalidade natureza) => natureza switch
    {
        NaturezaLegalModalidade.CotaReservada => CotaReservada,
        NaturezaLegalModalidade.Ampla => Ampla,
        NaturezaLegalModalidade.Suplementar => Suplementar,
        NaturezaLegalModalidade.OutraModalidade => OutraModalidade,
        NaturezaLegalModalidade.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(natureza), natureza, "NaturezaLegalModalidade.Nenhuma é sentinela e não tem token canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(natureza), natureza, "NaturezaLegalModalidade desconhecida."),
    };

    public static NaturezaLegalModalidade FromCodigo(string? codigo) => codigo switch
    {
        CotaReservada => NaturezaLegalModalidade.CotaReservada,
        Ampla => NaturezaLegalModalidade.Ampla,
        Suplementar => NaturezaLegalModalidade.Suplementar,
        OutraModalidade => NaturezaLegalModalidade.OutraModalidade,
        _ => NaturezaLegalModalidade.Nenhuma,
    };
}
