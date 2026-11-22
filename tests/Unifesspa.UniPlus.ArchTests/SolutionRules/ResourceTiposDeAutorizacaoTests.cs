namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Authorization.Contracts;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ResourceTiposDeAutorizacaoTests
{
    [Fact]
    public void TiposUsados_SeguemAConvencao()
    {
        Dictionary<string, List<string>> tipos = TiposPorModulo();
        tipos.Should().NotBeEmpty();
        ForaDaConvencao(tipos).Should().BeEmpty();
    }

    [Fact]
    public void TiposUsados_NaoSeRepetemEntreModulos() =>
        Repetidos(TiposPorModulo()).Should().BeEmpty();

    [Fact]
    public void Conferencia_RecusaTipoForaDaConvencao() =>
        ForaDaConvencao(new() { ["Selecao"] = ["TipoInvalido"] }).Should().ContainSingle();

    [Fact]
    public void Conferencia_RecusaTipoRepetidoEntreModulos() =>
        Repetidos(new() { ["Selecao"] = ["modalidade"], ["Configuracao"] = ["modalidade"] }).Should().ContainSingle();

    private static Dictionary<string, List<string>> TiposPorModulo()
    {
        IEnumerable<(string Modulo, string Tipo)> metodos = MethodMembers()
            .That().AreDeclaredIn(Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\."))
            .GetObjects(ArquiteturaDasTravas.Produto)
            .SelectMany(static metodo => TiposDoAtributo(metodo.Assembly.Name, metodo.AttributeInstances));
        IEnumerable<(string Modulo, string Tipo)> classes = Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.")
            .GetObjects(ArquiteturaDasTravas.Produto)
            .SelectMany(static tipo => TiposDoAtributo(tipo.Assembly.Name, tipo.AttributeInstances));
        return metodos.Concat(classes).GroupBy(static uso => uso.Modulo)
            .ToDictionary(static grupo => grupo.Key, static grupo => grupo.Select(static uso => uso.Tipo).Distinct().ToList());
    }

    private static IEnumerable<(string Modulo, string Tipo)> TiposDoAtributo(string modulo, IEnumerable<AttributeInstance> atributos) =>
        atributos.SelectMany(static atributo => atributo.AttributeArguments.OfType<AttributeNamedArgument>())
            .Where(static argumento => argumento.Name == "ResourceTipo" && argumento.Value is string)
            .Select(argumento => (modulo, (string)argumento.Value));

    private static string[] ForaDaConvencao(Dictionary<string, List<string>> tipos) =>
        [.. tipos.SelectMany(static par => par.Value.Where(static tipo => !ResourceTipos.SegueAConvencao(tipo)))];

    private static string[] Repetidos(Dictionary<string, List<string>> tipos) =>
        [.. tipos.SelectMany(static par => par.Value.Select(tipo => (par.Key, Tipo: tipo)))
            .GroupBy(static uso => uso.Tipo).Where(static grupo => grupo.Count() > 1).Select(static grupo => grupo.Key)];
}
