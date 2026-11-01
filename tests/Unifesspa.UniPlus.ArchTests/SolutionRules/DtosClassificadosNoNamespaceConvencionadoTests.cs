namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Projecao;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// O serializador defensivo da projeção (ADR-0153) vale para todo tipo, global e por tipo; por isso o DTO que declara
/// classificação, por <c>[ProtegidoComo]</c> ou por derivar de <c>ItemClassificado</c>, vive no namespace convencionado
/// <c>*.DTOs.Administrativos</c> do módulo, onde a convenção o torna reconhecível.
/// </summary>
public sealed class DtosClassificadosNoNamespaceConvencionadoTests
{
    private static readonly string ItemClassificadoBase = typeof(ItemClassificado).FullName!;

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(AssembliesDoProduto()).Build();

    [Fact(DisplayName = "Todo tipo com propriedade [ProtegidoComo] ou derivado de ItemClassificado fica em *.DTOs.Administrativos")]
    public void DtoClassificado_FicaNoNamespaceConvencionado()
    {
        IType[] comPropriedadeProtegida =
        [
            .. PropertyMembers().That().HaveAnyAttributes(typeof(ProtegidoComoAttribute)).GetObjects(Arquitetura).Select(static p => p.DeclaringType),
        ];
        IType[] derivados =
        [
            .. Types().That().AreAssignableTo(typeof(ItemClassificado)).GetObjects(Arquitetura)
                .Where(static t => t.FullName != ItemClassificadoBase),
        ];

        comPropriedadeProtegida.Should().NotBeEmpty("sem DTO descoberto, a conferência passaria vazia");
        derivados.Should().NotBeEmpty("sem item classificado descoberto, a conferência passaria vazia");

        string[] fora =
        [
            .. comPropriedadeProtegida.Concat(derivados)
                .Where(static t => !t.Namespace.FullName.EndsWith(".DTOs.Administrativos", StringComparison.Ordinal))
                .Select(static t => t.FullName)
                .Distinct(),
        ];
        fora.Should().BeEmpty("o DTO classificado fica em *.DTOs.Administrativos do módulo. Fora da convenção:\n" + string.Join("\n", fora));
    }

    private static ReflectionAssembly[] AssembliesDoProduto() =>
        [
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal))
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}
