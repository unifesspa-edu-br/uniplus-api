namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Se o candidato fez a prova de uma área do ENEM.</summary>
public enum SituacaoDaProvaNoEnem
{
    [CodigoDeContrato("PRESENTE", "Presente", Descricao = "O candidato fez a prova da área.")]
    Presente = 1,

    /// <summary>Faltou à prova da área. A nota que vem no arquivo é zero e não vale como nota obtida.</summary>
    [CodigoDeContrato("AUSENTE", "Ausente", Descricao = "O candidato faltou à prova da área.")]
    Ausente = 2,
}
