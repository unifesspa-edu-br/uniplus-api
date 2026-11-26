namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como o edital prevê a manifestação de interesse das pessoas que continuam concorrendo depois da 1ª chamada
/// (UNI-REQ-0503). Quem declara um dos valores prevê a manifestação e a exige; o processo sem valor não a prevê, e
/// não existe valor para "não prevista". O prazo da manifestação é da publicação de cada chamada, não desta declaração.
/// </summary>
public enum ManifestacaoDeInteresse
{
    /// <summary>A pessoa manifesta interesse uma vez, no prazo da chamada, e a manifestação vale para as chamadas seguintes.</summary>
    [CodigoDeContrato("UMA_VEZ", "Manifesta interesse uma só vez",
        Descricao = "A pessoa manifesta interesse uma vez depois da 1ª chamada, e a manifestação vale para as chamadas seguintes; quem volta a aguardar não manifesta de novo.")]
    UmaVez = 1,

    /// <summary>Além de manifestar, a pessoa confirma o interesse a cada chamada.</summary>
    [CodigoDeContrato("CONFIRMA_A_CADA_CHAMADA", "Confirma o interesse a cada chamada",
        Descricao = "A pessoa manifesta interesse depois da 1ª chamada e confirma o interesse a cada chamada seguinte.")]
    ConfirmaACadaChamada = 2,
}
