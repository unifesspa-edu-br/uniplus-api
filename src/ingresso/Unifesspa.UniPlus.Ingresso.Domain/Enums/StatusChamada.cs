namespace Unifesspa.UniPlus.Ingresso.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma chamada. O código de cada membro é o valor gravado na coluna <c>status</c>.</summary>
[DescricaoDoContrato("A situação de uma chamada de convocação: agendada antes de começar, em andamento enquanto os candidatos se manifestam, concluída ou cancelada.")]
public enum StatusChamada
{
    [CodigoDeContrato("AGENDADA", "Agendada", Descricao = "A chamada foi criada e ainda não começou.")]
    Agendada = 1,
    [CodigoDeContrato("EM_ANDAMENTO", "Em andamento", Descricao = "A chamada começou e os candidatos convocados podem se manifestar.")]
    EmAndamento = 2,
    [CodigoDeContrato("CONCLUIDA", "Concluída", Descricao = "A chamada terminou.")]
    Concluida = 3,
    [CodigoDeContrato("CANCELADA", "Cancelada", Descricao = "A chamada foi cancelada antes de terminar.")]
    Cancelada = 4
}
