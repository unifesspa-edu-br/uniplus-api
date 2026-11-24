namespace Unifesspa.UniPlus.Discentes.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Situação de uma execução da sincronização com o SIGAA. O código de cada membro é o valor gravado na coluna <c>status</c>.</summary>
[DescricaoDoContrato("A situação de uma execução da sincronização de dados discentes com o SIGAA: em andamento enquanto roda, e ao terminar concluída, parcial ou falhou.")]
public enum SyncRunStatus
{
    [CodigoDeContrato("EM_ANDAMENTO", "Em andamento", Descricao = "A execução começou e ainda não terminou.")]
    EmAndamento = 1,
    [CodigoDeContrato("CONCLUIDA", "Concluída", Descricao = "A execução terminou e aproveitou os dados recebidos.")]
    Concluida = 2,
    [CodigoDeContrato("PARCIAL", "Parcial", Descricao = "A execução terminou, mas só parte dos dados foi gravada.")]
    Parcial = 3,
    [CodigoDeContrato("FALHOU", "Falhou", Descricao = "A execução terminou sem aproveitar nenhum dado.")]
    Falhou = 4
}
