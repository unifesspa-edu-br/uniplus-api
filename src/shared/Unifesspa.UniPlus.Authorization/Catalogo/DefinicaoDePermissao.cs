namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>Definição de uma permissão lida do atributo de um membro de enum.</summary>
/// <param name="Codigo">Código <c>{módulo}:{resource}:{ação}</c>.</param>
/// <param name="Nome">Nome legível.</param>
/// <param name="Descricao">Descrição do que a permissão autoriza.</param>
/// <param name="Modulo">Módulo dono da permissão.</param>
/// <param name="Resource">Resource sobre o qual a permissão age.</param>
/// <param name="Acao">Ação que a permissão autoriza.</param>
/// <param name="Requisito">Requisito que o ponto de decisão avalia.</param>
/// <param name="ReservadaAGovernanca">Só perfis de governança podem reunir a permissão.</param>
public sealed record DefinicaoDePermissao(
    string Codigo,
    string Nome,
    string Descricao,
    string Modulo,
    string Resource,
    string Acao,
    PermissionRequirement Requisito,
    bool ReservadaAGovernanca = false);
