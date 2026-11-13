namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Depois da primeira designação, a instituição não fica sem nenhuma pessoa com concessão vigente de Representante do
/// controlador, porque sem ela param a etapa da proteção de dados dos pedidos, as versões protegidas de perfil, a janela
/// de migração e a proteção do catálogo de tipos de documento e de fatos do candidato (UNI-REQ-0394). A troca se faz
/// designando primeiro quem substitui e revogando depois. A primeira designação não depende desta regra: ela entra pela
/// dispensa, quando não há Representante vigente (ADR-0162).
/// </summary>
public static class ContinuidadeDoRepresentante
{
    /// <summary>
    /// A concessão é uma designação vigente de Representante: do perfil dele, da instituição e ativa no instante. Só a
    /// revogação de uma dessas reduz o número de Representantes; a vencida ou já revogada não muda a conta.
    /// </summary>
    public static bool EDesignacaoVigente(Concessao concessao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return concessao.PerfilId == PerfisDeSistema.RepresentanteDoControladorId
            && concessao.EscopoTipo == TipoDeEscopo.Instituicao
            && concessao.EstaAtiva(agora);
    }

    /// <summary>
    /// A revogação da concessão deixaria a instituição sem Representante vigente: ela é designação vigente e nenhuma outra
    /// pessoa tem concessão vigente do papel. A contagem é literal: a concessão anterior a esta regra, com prazo ainda não
    /// vencido, conta como vigente.
    /// </summary>
    /// <param name="concessao">A concessão que se revoga.</param>
    /// <param name="agora">O instante da revogação.</param>
    /// <param name="haOutraPessoaVigente">Outra pessoa, que não o beneficiário, tem concessão vigente de Representante.</param>
    public static bool RevogacaoDeixariaSemRepresentante(Concessao concessao, DateTimeOffset agora, bool haOutraPessoaVigente) =>
        !haOutraPessoaVigente && EDesignacaoVigente(concessao, agora);
}
