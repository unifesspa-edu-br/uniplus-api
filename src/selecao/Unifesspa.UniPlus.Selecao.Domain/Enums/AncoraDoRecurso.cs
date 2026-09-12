namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// De que instante o prazo de interposição corre.
/// </summary>
/// <remarks>
/// São dois relógios distintos. <see cref="AtoPublicado"/> é um instante só, o da publicação,
/// e vale igual para todos os candidatos. <see cref="CienciaIndividual"/> corre por candidato,
/// do momento em que cada um toma ciência da decisão que o alcança — é o caso do documento
/// declarado incapaz de comprovar o direito alegado, que não gera publicação nenhuma.
/// </remarks>
public enum AncoraDoRecurso
{
    /// <summary>Ausência declarada — recusada por <c>RecursoDaEtapa.Criar</c>.</summary>
    Nenhuma = 0,

    /// <summary>Do instante de publicação do ato (UNI-REQ-0115).</summary>
    AtoPublicado = 1,

    /// <summary>Da ciência do candidato sobre a decisão individual.</summary>
    CienciaIndividual = 2,
}
