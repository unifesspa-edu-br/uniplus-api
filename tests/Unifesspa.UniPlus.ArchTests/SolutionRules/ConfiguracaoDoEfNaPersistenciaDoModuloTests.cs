namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O mapeamento das entidades para o banco é detalhe da persistência: toda implementação de
/// <c>IEntityTypeConfiguration&lt;&gt;</c> mora em <c>{Modulo}.Infrastructure.Persistence</c>, ou num subnamespace dele, e
/// as da plataforma em <c>Infrastructure.Core.Persistence</c> (ADR-0170). O contexto único monta o modelo juntando as
/// contribuições de cada módulo, e um mapeamento fora do lugar escaparia dessa composição ou arrastaria o domínio ao EF.
/// </summary>
public sealed class ConfiguracaoDoEfNaPersistenciaDoModuloTests
{
    private const string PersistenciaDaPlataforma = @"Unifesspa\.UniPlus\.Infrastructure\.Core\.Persistence";

    [Fact(DisplayName = "Toda configuração do EF mora na Infrastructure.Persistence do módulo ou na da plataforma")]
    public void Produto_ConfiguracoesMoramNaPersistencia()
    {
        ConfiguracoesDoEf(ArquiteturaDasTravas.Produto).Should().NotBeEmpty("sem configuração descoberta, a conferência passaria vazia");

        ConfiguracoesFora(ArquiteturaDasTravas.Produto, ModulosDoMonolito.Raizes).Should().BeEmpty(
            "o mapeamento do EF fica na persistência da infraestrutura do módulo");
    }

    [Fact(DisplayName = "Fitness_ConfiguracaoForaDaPersistencia_Falha")]
    public void Fitness_ConfiguracaoForaDaPersistencia_Falha()
    {
        List<string> reprovados = ConfiguracoesFora(ArquiteturaDasTravas.Fixtures, ModulosDoMonolito.RaizesDosFixtures);

        reprovados.Should().Contain(static v => v.Contains("MapeamentoNoDominio", StringComparison.Ordinal));
        reprovados.Should().NotContain(static v => v.Contains("MapeamentoNaPersistencia", StringComparison.Ordinal));
    }

    private static List<Class> ConfiguracoesDoEf(Architecture arquitetura) =>
        ImplementadoresDeInterface.Em(
            arquitetura,
            static i => i.FullName.StartsWith("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration", StringComparison.Ordinal));

    private static List<string> ConfiguracoesFora(Architecture arquitetura, string[] raizes) =>
        ArquiteturaDasTravas.Reprovacoes(
            Types().That().Are(ConfiguracoesDoEf(arquitetura)).Should().ResideInNamespaceMatching(Permitido(raizes)),
            arquitetura);

    private static string Permitido(string[] raizes) =>
        $@"^({string.Join('|', raizes.Select(static r => $@"{Regex.Escape(r)}\.Infrastructure\.Persistence"))}|{PersistenciaDaPlataforma})(\.|$)";
}
