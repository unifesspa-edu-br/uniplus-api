namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Por que os gestores da instalação receberam a concessão sem aceitar o termo de responsabilidade.</summary>
public enum SituacaoDoTermoNaInstalacao
{
    /// <summary>A instalação antecedeu a primeira publicação do termo: não havia termo a aceitar.</summary>
    [CodigoDeContrato("SEM_TERMO_PUBLICADO", "Sem termo publicado")]
    SemTermoPublicado = 1,

    /// <summary>Estado anômalo: já havia versão vigente, e a instalação a registrou sem exigir aceite (ADR-0162).</summary>
    [CodigoDeContrato("TERMO_VIGENTE_SEM_ACEITE", "Termo vigente sem aceite")]
    TermoVigenteSemAceite = 2,
}
