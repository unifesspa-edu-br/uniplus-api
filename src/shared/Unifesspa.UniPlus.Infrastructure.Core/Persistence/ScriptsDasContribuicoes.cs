namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using System.Reflection;

/// <summary>De onde a preparaÃ§Ã£o do banco lê os scripts SQL embutidos.</summary>
public static class ScriptsDasContribuicoes
{
    /// <summary>
    /// O assembly do conjunto compartilhado da plataforma, que roda primeiro, e depois o de cada contribuição, na ordem
    /// recebida. Script de um módulo não referencia o esquema de outro.
    /// </summary>
    /// <param name="modulos">As contribuições, na ordem do contexto.</param>
    public static IReadOnlyList<Assembly> Assemblies(IEnumerable<IContribuicaoDoModulo> modulos)
    {
        ArgumentNullException.ThrowIfNull(modulos);

        return
        [
            typeof(UniPlusDbContext).Assembly,
            .. modulos.Select(static m => m.GetType().Assembly).Distinct(),
        ];
    }
}
