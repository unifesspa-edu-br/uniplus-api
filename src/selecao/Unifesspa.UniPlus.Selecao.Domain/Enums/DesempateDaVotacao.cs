namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como a banca resolve o empate nos critérios de maioria.</summary>
public enum DesempateDaVotacao
{
    [CodigoDeContrato("votoDaPresidencia", "Voto da presidência")]
    VotoDaPresidencia = 1,
    [CodigoDeContrato("novaVotacao", "Nova votação")]
    NovaVotacao = 2,
}
