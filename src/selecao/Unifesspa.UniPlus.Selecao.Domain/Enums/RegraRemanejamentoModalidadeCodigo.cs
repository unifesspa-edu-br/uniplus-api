namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="RegraRemanejamentoModalidade"/> e o token
/// textual exposto por <c>ModalidadeView.RegraRemanejamento</c>
/// (Configuracao.Contracts, ADR-0056). O token é opcional na origem — ausência
/// mapeia para <see cref="RegraRemanejamentoModalidade.Nenhuma"/> (modalidade
/// que não remaneja).
/// </summary>
public static class RegraRemanejamentoModalidadeCodigo
{
    public const string SegueCascata = "SEGUE_CASCATA";
    public const string DestinoUnico = "DESTINO_UNICO";
    public const string Cruzado = "CRUZADO";

    /// <summary>Token nulo ou não reconhecido mapeia para <see cref="RegraRemanejamentoModalidade.Nenhuma"/>.</summary>
    /// <summary>
    /// Converte o enum local de volta ao token cross-módulo publicado pelo catálogo
    /// de modalidades. Diferente das demais projeções, devolve <see langword="null"/>
    /// para <see cref="RegraRemanejamentoModalidade.Nenhuma"/>: ali o sentinela é um
    /// estado legítimo — a modalidade que não remaneja —, e é assim que a origem o
    /// representa (<c>ModalidadeView.RegraRemanejamento</c> é opcional). Emitir um
    /// token próprio inventaria vocabulário que o catálogo não publica, e o cliente
    /// que cruza as duas fontes voltaria a comparar valores que nunca casam.
    /// </summary>
    public static string? ToCodigo(this RegraRemanejamentoModalidade regra) => regra switch
    {
        RegraRemanejamentoModalidade.SegueCascata => SegueCascata,
        RegraRemanejamentoModalidade.DestinoUnico => DestinoUnico,
        RegraRemanejamentoModalidade.Cruzado => Cruzado,
        RegraRemanejamentoModalidade.Nenhuma => null,
        _ => throw new ArgumentOutOfRangeException(nameof(regra), regra, "RegraRemanejamentoModalidade desconhecida."),
    };

    public static RegraRemanejamentoModalidade FromCodigo(string? codigo) => codigo switch
    {
        SegueCascata => RegraRemanejamentoModalidade.SegueCascata,
        DestinoUnico => RegraRemanejamentoModalidade.DestinoUnico,
        Cruzado => RegraRemanejamentoModalidade.Cruzado,
        _ => RegraRemanejamentoModalidade.Nenhuma,
    };
}
