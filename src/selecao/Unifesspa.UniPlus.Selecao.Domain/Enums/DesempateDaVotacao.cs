namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a banca resolve o empate nos critérios de maioria.</summary>
public enum DesempateDaVotacao
{
    [CodigoDeContrato("VOTO_DA_PRESIDENCIA", "Voto da presidência")]
    VotoDaPresidencia = 1,
    [CodigoDeContrato("NOVA_VOTACAO", "Nova votação")]
    NovaVotacao = 2,
}
