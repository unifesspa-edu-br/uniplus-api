namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Escolha sobre a validade do documento.")]
public enum EscolhaDaValidade
{
    [CodigoDeContrato("NAO_EXIGE_DOCUMENTO_VALIDO", "Não exige documento válido")]
    NaoExigeDocumentoValido = 1,

    [CodigoDeContrato("EXIGE_DOCUMENTO_VALIDO", "Exige documento válido")]
    ExigeDocumentoValido = 2,
}
