namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O efeito suspensivo que o edital escolhe para o recurso da 1ª instância. A 2ª instância não tem escolha: sempre tem
/// efeito suspensivo.
/// </summary>
public enum EfeitoSuspensivo
{
    [CodigoDeContrato("semEfeitoSuspensivo", "Sem efeito suspensivo")]
    SemEfeitoSuspensivo = 1,
}
