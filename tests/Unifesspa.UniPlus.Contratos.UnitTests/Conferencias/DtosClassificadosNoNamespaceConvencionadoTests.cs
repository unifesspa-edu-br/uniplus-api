namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Projecao;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// O serializador defensivo da projeção (ADR-0153) vale para todo tipo, global e por tipo; por isso o DTO que declara
/// classificação, por <c>[ProtegidoComo]</c> ou por derivar de <c>ItemClassificado</c>, vive no namespace convencionado
/// <c>*.DTOs.Administrativos</c> do módulo, onde a convenção o torna reconhecível.
/// </summary>
public sealed class DtosClassificadosNoNamespaceConvencionadoTests
{
    private static readonly Type[] Tipos = [.. AssembliesDoProduto().SelectMany(static assembly => assembly.GetTypes())];

    [Fact(DisplayName = "Todo tipo com propriedade [ProtegidoComo] ou derivado de ItemClassificado fica em *.DTOs.Administrativos")]
    public void DtoClassificado_FicaNoNamespaceConvencionado()
    {
        Type[] comPropriedadeProtegida = [.. Tipos.Where(TemPropriedadeProtegida)];
        Type[] derivados = [.. Tipos.Where(static tipo => tipo != typeof(ItemClassificado) && typeof(ItemClassificado).IsAssignableFrom(tipo))];

        comPropriedadeProtegida.Should().NotBeEmpty("sem DTO descoberto, a conferência passaria vazia");
        derivados.Should().NotBeEmpty("sem item classificado descoberto, a conferência passaria vazia");

        string[] fora =
        [
            .. comPropriedadeProtegida.Concat(derivados)
                .Where(static t => !(t.Namespace ?? string.Empty).EndsWith(".DTOs.Administrativos", StringComparison.Ordinal))
                .Select(static t => t.FullName!)
                .Distinct(),
        ];
        fora.Should().BeEmpty("o DTO classificado fica em *.DTOs.Administrativos do módulo. Fora da convenção:\n" + string.Join("\n", fora));
    }

    private static bool TemPropriedadeProtegida(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(static propriedade => propriedade.IsDefined(typeof(ProtegidoComoAttribute), false));

    private static ReflectionAssembly[] AssembliesDoProduto() =>
        [
            .. Directory.GetFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.dll")
                .Where(static c => !Path.GetFileName(c).Contains("Tests", StringComparison.Ordinal))
                .Select(static c => ReflectionAssembly.LoadFrom(c)),
        ];
}
