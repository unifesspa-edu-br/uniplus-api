namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Porta pela qual o perfil confere as permissões que reúne contra o catálogo da aplicação.
/// O domínio não conhece o projeto de autorização que mantém o catálogo; a infraestrutura
/// implementa esta porta a partir dele.
/// </summary>
public interface ICatalogoDePermissoesConsultavel
{
    /// <summary>O código consta do catálogo.</summary>
    bool Existe(string codigo);

    /// <summary>A permissão só pode ser reunida por perfis de governança.</summary>
    bool ReservadaAGovernanca(string codigo);

    /// <summary>
    /// A permissão é de unidade: o catálogo exige a unidade proprietária do resource no contexto da
    /// decisão. As demais permissões são institucionais.
    /// </summary>
    bool DeUnidade(string codigo);

    /// <summary>
    /// A sensibilidade que o catálogo atribui à permissão. Código que o catálogo não conhece conta como a mais
    /// restrita: o perfil não pode parecer menos sensível por causa de uma lacuna.
    /// </summary>
    ClassificacaoDeProtecao Sensibilidade(string codigo);
}
