namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Qualifica um membro de enum de permissões: o código que é o contrato, o texto
/// legível e os metadados que condicionam a decisão de acesso (ADR-0078, ADR-0151).
/// </summary>
/// <remarks>
/// O código é declarado à mão e nunca derivado do nome do membro: ele é o nome do
/// papel no provedor de identidade, e renomear o membro não pode mudá-lo.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class PermissaoAttribute : Attribute
{
    /// <summary>Cria a qualificação de uma permissão.</summary>
    /// <param name="codigo">Código <c>{módulo}:{recurso}:{ação}</c>.</param>
    /// <param name="nome">Nome legível, em pt-BR.</param>
    /// <param name="descricao">Descrição do que a permissão autoriza, em pt-BR.</param>
    /// <param name="sensibilidade">Sensibilidade do dado que a operação retorna.</param>
    public PermissaoAttribute(string codigo, string nome, string descricao, Sensibilidade sensibilidade)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        Sensibilidade = sensibilidade;
    }

    /// <summary>Código da permissão.</summary>
    public string Codigo { get; }

    /// <summary>Nome legível.</summary>
    public string Nome { get; }

    /// <summary>Descrição do que a permissão autoriza.</summary>
    public string Descricao { get; }

    /// <summary>Sensibilidade do dado que a operação retorna.</summary>
    public Sensibilidade Sensibilidade { get; }

    /// <summary>Base legal padrão. Vazia quando o dado é público.</summary>
    public string? BaseLegal { get; init; }

    /// <summary>A permissão exige multifator satisfeito.</summary>
    public bool RequerMfa { get; init; }

    /// <summary>A permissão exige dupla aprovação válida.</summary>
    public bool RequerDuplaAprovacao { get; init; }

    /// <summary>Campos do contexto do recurso que a permissão exige presentes.</summary>
    public string[]? EscopoContextoObrigatorio { get; init; }

    /// <summary>Verificações contextuais adicionais que a decisão orquestra.</summary>
    public string[]? VerificacoesDeContexto { get; init; }

    /// <summary>
    /// A permissão só pode ser reunida por perfis de governança. Um perfil comum que a
    /// reunisse criaria, na prática, um gestor de acessos sem o ato de designação e fora
    /// da conta do mínimo de gestores.
    /// </summary>
    public bool ReservadaAGovernanca { get; init; }
}
