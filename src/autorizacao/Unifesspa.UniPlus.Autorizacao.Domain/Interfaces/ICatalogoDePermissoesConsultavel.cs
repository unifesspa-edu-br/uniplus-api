namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

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
}
