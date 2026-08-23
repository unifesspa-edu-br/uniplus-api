namespace Unifesspa.UniPlus.Authorization;

using System.Reflection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Permissões institucionais do Uni+ e os metadados que condicionam cada
/// decisão de acesso sobre elas (ADR-0078).
/// </summary>
/// <remarks>
/// <para>
/// O código de uma permissão é <c>{módulo}:{recurso}:{ação}</c>, em minúsculas.
/// Acrescentar uma permissão é acrescentar aqui um par de membros — a constante
/// com o código e o requisito com os metadados — e incluí-lo em
/// <see cref="Todas"/>.
/// </para>
/// <para>
/// Os metadados são validados por <see cref="PermissionRequirement.From"/>, que
/// é a fábrica do contrato: um requisito mal formado não chega a existir. O
/// vocabulário de <see cref="PermissionRequirement.EscopoContextoObrigatorio"/>
/// e de <see cref="PermissionRequirement.VerificacoesDeContexto"/> é conferido
/// contra o que o ponto de decisão sabe verificar, por teste.
/// </para>
/// </remarks>
public static class UniPlusPermissions
{
    /// <summary>Manter os motivos de decisão recursal.</summary>
    public const string ConfiguracaoMotivosDecisaoRecursalManter =
        "configuracao:motivos-decisao-recursal:manter";

    /// <summary>Consultar a trilha protegida dos motivos de decisão recursal.</summary>
    public const string ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria =
        "configuracao:motivos-decisao-recursal:consultar-auditoria";

    /// <summary>Requisito de <see cref="ConfiguracaoMotivosDecisaoRecursalManter"/>.</summary>
    public static PermissionRequirement ConfiguracaoMotivosDecisaoRecursalManterRequirement { get; } =
        PermissionRequirement.From(
            ConfiguracaoMotivosDecisaoRecursalManter,
            Sensibilidade.Interna).Value!;

    /// <summary>Requisito de <see cref="ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria"/>.</summary>
    public static PermissionRequirement ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement { get; } =
        PermissionRequirement.From(
            ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria,
            Sensibilidade.Interna).Value!;

    /// <summary>Todas as permissões, para conferências que precisam percorrê-las.</summary>
    /// <remarks>
    /// Derivada das próprias propriedades da classe, e não mantida como uma
    /// segunda lista: uma permissão acrescentada e esquecida aqui ficaria fora de
    /// toda conferência — o campo de contexto desconhecido, a verificação sem
    /// implementação e o código fora do formato deixariam de ser detectados
    /// justamente na permissão nova, sem nada acusar.
    /// </remarks>
    public static IReadOnlyList<PermissionRequirement> Todas { get; } =
        [.. typeof(UniPlusPermissions)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(static propriedade => propriedade.PropertyType == typeof(PermissionRequirement))
            .Select(static propriedade => (PermissionRequirement)propriedade.GetValue(null)!)
            .OrderBy(static requisito => requisito.Permissao, StringComparer.Ordinal)];
}
