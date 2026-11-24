namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O evento a partir do qual corre o prazo de interposição do recurso.
/// </summary>
/// <remarks>
/// São dois relógios distintos. <see cref="PublicacaoDoAto"/> é um instante só, o da publicação,
/// e vale igual para todos os candidatos. <see cref="CienciaIndividual"/> corre por candidato,
/// do momento em que cada um toma ciência da decisão que o alcança — é o caso do documento
/// declarado incapaz de comprovar o direito alegado, que não gera publicação nenhuma.
/// </remarks>
public enum EventoInicialDoPrazo
{
    [CodigoDeContrato(
        "PUBLICACAO_DO_ATO",
        "Publicação do ato",
        Descricao = "O prazo conta da publicação do ato e vale igual para todos os candidatos (UNI-REQ-0115).")]
    PublicacaoDoAto = 1,

    [CodigoDeContrato(
        "CIENCIA_INDIVIDUAL",
        "Ciência individual do candidato",
        Descricao = "O prazo conta da data em que cada candidato toma ciência da decisão que o alcança.")]
    CienciaIndividual = 2,
}
