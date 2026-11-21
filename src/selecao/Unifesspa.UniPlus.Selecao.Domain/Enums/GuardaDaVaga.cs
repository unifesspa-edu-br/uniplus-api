namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Se a vaga em disputa fica guardada enquanto o recurso corre. Com instância superior a vaga fica sempre guardada até o
/// resultado definitivo, e <see cref="NaoGuarda"/> só cabe no recurso que não admite instância superior.
/// </summary>
public enum GuardaDaVaga
{
    [CodigoDeContrato(
        "ateOResultadoDefinitivo",
        "Guarda a vaga até o resultado definitivo",
        Descricao = "Se quem recorre seria chamado para a vaga, ela fica guardada até a publicação do resultado definitivo.")]
    AteOResultadoDefinitivo = 1,

    [CodigoDeContrato(
        "naoGuarda",
        "Não guarda a vaga",
        Descricao = "A vaga não fica guardada durante o recurso. Se o recurso for deferido com a vaga já ocupada, a unidade resolve a situação.")]
    NaoGuarda = 2,
}
