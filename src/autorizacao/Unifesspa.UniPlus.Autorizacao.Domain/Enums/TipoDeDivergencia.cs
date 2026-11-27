namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que o perfil de governança tem de diferente do esperado pelo código ou do catálogo anterior.</summary>
public enum TipoDeDivergencia
{
    /// <summary>O código espera uma permissão que o perfil não reúne.</summary>
    [CodigoDeContrato("COMPOSICAO_FALTANTE", "Composição faltante")]
    ComposicaoFaltante = 1,

    /// <summary>O perfil reúne uma permissão que o código não espera.</summary>
    [CodigoDeContrato("COMPOSICAO_EXCEDENTE", "Composição excedente")]
    ComposicaoExcedente = 2,

    /// <summary>A classificação de uma permissão do perfil mudou no catálogo.</summary>
    [CodigoDeContrato("CLASSIFICACAO", "Classificação")]
    Classificacao = 3,

    /// <summary>O escopo de uma permissão do perfil mudou no catálogo.</summary>
    [CodigoDeContrato("ESCOPO", "Escopo")]
    Escopo = 4,
}
