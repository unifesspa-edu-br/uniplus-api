namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Em que pé estão as modalidades em que o candidato concorre, diante das respostas dele (UNI-REQ-0076).</summary>
public enum SituacaoDasModalidades
{
    /// <summary>As respostas levam a ao menos uma das modalidades ofertadas.</summary>
    [CodigoDeContrato("CONCORRE", "Concorre", Descricao = "As respostas indicam modalidades ofertadas.")]
    Concorre = 1,

    /// <summary>As respostas já decidem a derivação, e nenhuma modalidade ofertada se aplica.</summary>
    [CodigoDeContrato("NAO_CONCORRE", "Não concorre", Descricao = "Nenhuma modalidade ofertada se aplica.")]
    NaoConcorre = 2,

    /// <summary>Falta responder algo de que a derivação depende.</summary>
    [CodigoDeContrato("FALTA_RESPONDER", "Falta responder", Descricao = "Faltam respostas necessárias para determinar as modalidades.")]
    FaltaResponder = 3,
}
