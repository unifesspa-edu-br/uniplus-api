namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("Data usada como referência para o documento.")]
public enum TipoDaDataDeReferencia
{
    [CodigoDeContrato("FIM_DAS_INSCRICOES", "Fim das inscrições")]
    FimDasInscricoes = 1,

    [CodigoDeContrato("INICIO_DA_FASE", "Início da fase")]
    InicioDaFase = 2,

    [CodigoDeContrato("FIM_DA_FASE", "Fim da fase")]
    FimDaFase = 3,

    [CodigoDeContrato("DATA_ESPECIFICA", "Data específica")]
    DataEspecifica = 4,

    [CodigoDeContrato("DATA_DA_APRESENTACAO", "Data da apresentação")]
    DataDaApresentacao = 5,
}
