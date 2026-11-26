namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Sobre quem o dado do candidato é respondido (ADR-0136): o próprio
/// candidato, ou cada membro de uma lista do formulário, como a composição familiar (ADR-0138).
/// </summary>
[DescricaoDoContrato("Sobre quem o dado do candidato é respondido: o próprio candidato ou cada membro de uma lista do formulário.")]
public enum EscopoFato
{
    [CodigoDeContrato("CANDIDATO", "Candidato", Descricao = "O dado é respondido sobre o próprio candidato.")]
    Candidato = 1,

    [CodigoDeContrato(
        "MEMBRO_GRUPO",
        "Membro do grupo",
        Descricao = "O dado é respondido sobre cada membro de uma lista do formulário, como a composição familiar.")]
    MembroGrupo = 2,
}
