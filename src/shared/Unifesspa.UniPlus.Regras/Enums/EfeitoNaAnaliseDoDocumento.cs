namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Efeito na análise de um documento fora do prazo ou vencido.")]
public enum EfeitoNaAnaliseDoDocumento
{
    [CodigoDeContrato("RECUSAR_DOCUMENTO", "Recusar documento")]
    RecusarDocumento = 1,

    [CodigoDeContrato("ACEITAR_COM_AVISO", "Aceitar com aviso")]
    AceitarComAviso = 2,
}
