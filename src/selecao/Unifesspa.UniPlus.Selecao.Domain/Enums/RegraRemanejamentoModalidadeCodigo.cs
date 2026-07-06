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
    public static RegraRemanejamentoModalidade FromCodigo(string? codigo) => codigo switch
    {
        SegueCascata => RegraRemanejamentoModalidade.SegueCascata,
        DestinoUnico => RegraRemanejamentoModalidade.DestinoUnico,
        Cruzado => RegraRemanejamentoModalidade.Cruzado,
        _ => RegraRemanejamentoModalidade.Nenhuma,
    };
}
