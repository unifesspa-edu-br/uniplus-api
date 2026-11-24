namespace Unifesspa.UniPlus.Ingresso.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma matrícula. O código de cada membro é o valor gravado na coluna <c>status</c>.</summary>
[DescricaoDoContrato("A situação de uma matrícula: do pedido pendente, passando pelo envio e pela análise da documentação, até a efetivação, o indeferimento ou o cancelamento.")]
public enum StatusMatricula
{
    [CodigoDeContrato("PENDENTE", "Pendente", Descricao = "A matrícula foi aberta e a documentação ainda não foi enviada.")]
    Pendente = 1,
    [CodigoDeContrato("DOCUMENTACAO_ENVIADA", "Documentação enviada", Descricao = "O candidato enviou a documentação e ela aguarda análise.")]
    DocumentacaoEnviada = 2,
    [CodigoDeContrato("EM_ANALISE", "Em análise", Descricao = "A documentação está sendo analisada.")]
    EmAnalise = 3,
    [CodigoDeContrato("DEFERIDA", "Deferida", Descricao = "A documentação foi aceita.")]
    Deferida = 4,
    [CodigoDeContrato("INDEFERIDA", "Indeferida", Descricao = "A documentação foi recusada.")]
    Indeferida = 5,
    [CodigoDeContrato("EFETIVADA", "Efetivada", Descricao = "A matrícula foi concluída.")]
    Efetivada = 6,
    [CodigoDeContrato("CANCELADA", "Cancelada", Descricao = "A matrícula foi cancelada.")]
    Cancelada = 7
}
