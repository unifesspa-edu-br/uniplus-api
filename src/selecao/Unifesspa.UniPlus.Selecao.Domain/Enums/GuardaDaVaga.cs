namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Se a vaga em disputa fica guardada enquanto o recurso corre. Com instância superior a vaga fica sempre guardada até o
/// resultado definitivo, e <see cref="NaoGuarda"/> só cabe no recurso que não admite instância superior.
/// </summary>
public enum GuardaDaVaga
{
    [CodigoDeContrato("ateOResultadoDefinitivo", "Guarda a vaga até o resultado definitivo")]
    AteOResultadoDefinitivo = 1,

    [CodigoDeContrato("naoGuarda", "Não guarda a vaga")]
    NaoGuarda = 2,
}
