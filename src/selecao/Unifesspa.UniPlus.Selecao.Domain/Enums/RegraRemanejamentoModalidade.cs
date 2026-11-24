namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de RegraRemanejamentoModalidade.</summary>
public enum RegraRemanejamentoModalidade
{
    [CodigoDeContrato("SEGUE_ORDEM_DE_REMANEJAMENTO", "Segue a ordem de remanejamento", Descricao = "As vagas seguem a ordem legal de remanejamento.")]
    SegueOrdemDeRemanejamento = 1,

    [CodigoDeContrato("DESTINO_UNICO", "Destino único", Descricao = "As vagas seguem para uma modalidade de destino.")]
    DestinoUnico = 2,

    [CodigoDeContrato("CRUZADO", "Remanejamento cruzado", Descricao = "As vagas são remanejadas entre modalidades pareadas.")]
    Cruzado = 3,
}
