namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Escolha sobre o prazo de emissão do documento.")]
public enum EscolhaDoPrazoDeEmissao
{
    [CodigoDeContrato("SEM_PRAZO", "Sem prazo")]
    SemPrazo = 1,

    [CodigoDeContrato("COM_PRAZO", "Com prazo")]
    ComPrazo = 2,
}
