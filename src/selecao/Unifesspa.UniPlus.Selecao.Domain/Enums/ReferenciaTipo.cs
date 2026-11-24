namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Valores de ReferenciaTipo.</summary>
public enum ReferenciaTipo
{
    [CodigoDeContrato("FIM_INSCRICAO", "Fim da inscrição", Descricao = "Usa o fim da inscrição como referência.")]
    FimInscricao = 1,
    [CodigoDeContrato("INICIO_FASE", "Início da fase", Descricao = "Usa o início da fase como referência.")]
    InicioFase = 2,
    [CodigoDeContrato("FIM_FASE", "Fim da fase", Descricao = "Usa o fim da fase como referência.")]
    FimFase = 3,
    [CodigoDeContrato("DATA_ESPECIFICA", "Data específica", Descricao = "Usa a data informada como referência.")]
    DataEspecifica = 4,
}
