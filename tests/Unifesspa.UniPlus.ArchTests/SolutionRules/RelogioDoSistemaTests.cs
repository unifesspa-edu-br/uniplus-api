namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Tempo;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using Type = System.Type;

/// <summary>
/// O instante do sistema vem de um só relógio, o <c>RelogioEmMicrossegundos</c>, registrado na composição de cada
/// processo. Em produção, nenhum tipo lê o relógio estático (<c>DateTime</c> e <c>DateTimeOffset</c>: <c>UtcNow</c>,
/// <c>Now</c>, <c>Today</c>), só o relógio pega <c>TimeProvider.System</c>, e só o <c>Program</c> de cada processo
/// pega o relógio do sistema. A regra enxerga a chamada de membro, inclusive a da lambda, da função local, do método
/// assíncrono, do iterador, do construtor estático e dos inicializadores, e a atribui ao tipo de fora. As instruções de
/// nível superior do <c>Program</c> o analisador não enxerga: o que o <c>Program</c> chama por conta própria fica fora da
/// conferência, e ele entra só como a exceção que registra o relógio. O escopo são os assemblies de produto, com o
/// Verificador; os testes ficam fora.
/// </summary>
public sealed class RelogioDoSistemaTests
{
    private const string ProgramDoProcesso = "Program";

    private static readonly string ExcecaoDoRelogio = typeof(RelogioEmMicrossegundos).FullName!;
    private static readonly string ExcecaoDaFixture = typeof(FixturesDeRelogio.ExcecaoPermitida).FullName!;
    private static readonly string FixturesFullName = typeof(FixturesDeRelogio).FullName!;

    private static readonly (Type Tipo, string Membro)[] LeiturasDoRelogioEstatico =
    [
        (typeof(DateTime), "get_UtcNow()"), (typeof(DateTime), "get_Now()"), (typeof(DateTime), "get_Today()"),
        (typeof(DateTimeOffset), "get_UtcNow()"), (typeof(DateTimeOffset), "get_Now()"),
    ];

    private static readonly string[] FormasDeLerOEstatico =
    [
        nameof(FixturesDeRelogio.MetodoComum), nameof(FixturesDeRelogio.Lambda), nameof(FixturesDeRelogio.Closure),
        nameof(FixturesDeRelogio.FuncaoLocal), nameof(FixturesDeRelogio.FuncaoLocalComCaptura), nameof(FixturesDeRelogio.MetodoAsync),
        nameof(FixturesDeRelogio.LambdaDentroDeAsync), nameof(FixturesDeRelogio.Iterador), nameof(FixturesDeRelogio.ConstrutorEstatico),
        nameof(FixturesDeRelogio.InicializadorDeCampoEstatico), nameof(FixturesDeRelogio.InicializadorDeCampoDeInstancia),
        nameof(FixturesDeRelogio.InicializadorDePropriedade), nameof(FixturesDeRelogio.PropriedadeComExpressao),
    ];

    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies([typeof(object).Assembly, typeof(FixturesDeRelogio).Assembly, .. AssembliesDoProduto.Todos])
        .Build();

    private static readonly IType[] TiposDoProdutoCarregados = [.. TiposDoProduto().GetObjects(Arquitetura)];
    private static readonly IType[] TiposDasFixturesCarregados = [.. TiposDasFixtures().GetObjects(Arquitetura)];
    private static readonly MethodMember[] AlvosDoRelogioEstatico = [.. LeiturasDoRelogioEstatico.SelectMany(static l => Resolver(Membro(l.Tipo, l.Membro)))];
    private static readonly MethodMember[] AlvoSystemDoTimeProvider = Resolver(Membro(typeof(TimeProvider), "get_System()"));
    private static readonly MethodMember[] AlvoDoSistemaDoRelogio = Resolver(Membro(typeof(RelogioEmMicrossegundos), "get_DoSistema()"));

    [Fact(DisplayName = "As formas de ler o relógio estático são reprovadas, cada uma no tipo de fora")]
    public void Fixtures_LeituraDoRelogioEstatico_Falha()
    {
        string[] reprovados = Reprovados(TiposDasFixturesCarregados, null, AlvosDoRelogioEstatico);

        foreach (string forma in FormasDeLerOEstatico)
        {
            reprovados.Should().Contain($"{FixturesFullName}+{forma}", $"a forma {forma} deve ser reprovada");
        }
    }

    [Theory(DisplayName = "Cada leitura do relógio estático é reprovada sozinha")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Fixtures_CadaLeituraDoRelogioEstatico_Falha(int indice)
    {
        (Type tipo, string membro) = LeiturasDoRelogioEstatico[indice];

        Reprovados(TiposDasFixturesCarregados, null, Resolver(Membro(tipo, membro))).Should().Contain($"{FixturesFullName}+{nameof(FixturesDeRelogio.MetodoComum)}");
    }

    [Fact(DisplayName = "TimeProvider.System e o relógio do sistema, fora das exceções, são reprovados")]
    public void Fixtures_RelogioDoSistemaPorFora_Falha()
    {
        Reprovados(TiposDasFixturesCarregados, ExcecaoDaFixture, AlvoSystemDoTimeProvider)
            .Should().Contain($"{FixturesFullName}+{nameof(FixturesDeRelogio.MetodoComum)}");
        Reprovados(TiposDasFixturesCarregados, ExcecaoDaFixture, AlvoDoSistemaDoRelogio)
            .Should().ContainSingle().Which.Should().Be($"{FixturesFullName}+{nameof(FixturesDeRelogio.LeOrelogioDoSistemaForaDaComposicao)}");
    }

    [Fact(DisplayName = "O relógio injetado, o instante sem relógio e a exceção permitida passam")]
    public void Fixtures_ConformesPassam()
    {
        string[] reprovados =
        [
            .. Reprovados(TiposDasFixturesCarregados, ExcecaoDaFixture, AlvosDoRelogioEstatico),
            .. Reprovados(TiposDasFixturesCarregados, ExcecaoDaFixture, AlvoSystemDoTimeProvider),
            .. Reprovados(TiposDasFixturesCarregados, ExcecaoDaFixture, AlvoDoSistemaDoRelogio),
        ];

        reprovados.Should().NotContain($"{FixturesFullName}+{nameof(FixturesDeRelogio.UsaORelogioInjetado)}")
            .And.NotContain($"{FixturesFullName}+{nameof(FixturesDeRelogio.UsaDateTimeSemRelogio)}")
            .And.NotContain(ExcecaoDaFixture);
    }

    [Fact(DisplayName = "Em produção, nenhum tipo lê o relógio estático")]
    public void Produto_NaoLeORelogioEstatico()
    {
        TiposDoProdutoCarregados.Should().NotBeEmpty("sem tipo de produto carregado, a conferência passaria vazia");

        Reprovados(TiposDoProdutoCarregados, null, AlvosDoRelogioEstatico).Should().BeEmpty(
            "o instante do sistema vem do relógio injetado (TimeProvider.GetUtcNow()), nunca de DateTime(Offset).UtcNow/Now/Today");
    }

    [Fact(DisplayName = "Em produção, só o relógio em microssegundos pega TimeProvider.System")]
    public void Produto_SoORelogioPegaOTimeProviderDoSistema()
    {
        Types().That().Are(TiposDoProdutoCarregados).And().CallAny(AlvoSystemDoTimeProvider).GetObjects(Arquitetura).Select(static t => t.FullName)
            .Should().ContainSingle(static n => n.StartsWith(ExcecaoDoRelogio, StringComparison.Ordinal));

        Reprovados(TiposDoProdutoCarregados, ExcecaoDoRelogio, AlvoSystemDoTimeProvider).Should().BeEmpty(
            "o relógio do sistema é o RelogioEmMicrossegundos.DoSistema, registrado na composição do processo");
    }

    [Fact(DisplayName = "Em produção, só o Program de cada processo pega o relógio do sistema")]
    public void Produto_SoOProgramPegaORelogioDoSistema()
    {
        string[] programas = [.. Types().That().Are(TiposDoProdutoCarregados).And().HaveFullNameStartingWith(ProgramDoProcesso).GetObjects(Arquitetura).Select(static t => t.Assembly.Name)];
        programas.Should().Contain(["Unifesspa.UniPlus.Host", "Unifesspa.UniPlus.Portal.API", "Unifesspa.UniPlus.Verificador"]);

        Reprovados(TiposDoProdutoCarregados, ProgramDoProcesso, AlvoDoSistemaDoRelogio).Should().BeEmpty(
            "só a composição de cada processo registra o relógio; os demais recebem o TimeProvider injetado");
    }

    private static GivenMethodMembersConjunction Membro(Type tipo, string nome) =>
        MethodMembers().That().AreDeclaredIn(tipo).And().HaveName(nome);

    // A regra de chamada reavalia o provedor de membros a cada tipo conferido: os alvos são resolvidos uma só vez, aqui.
    private static MethodMember[] Resolver(GivenMethodMembersConjunction alvo) => [.. alvo.GetObjects(Arquitetura)];

    private static GivenTypesConjunction TiposDoProduto() =>
        Types().That().ResideInAssembly(AssembliesDoProduto.Todos[0], AssembliesDoProduto.Todos[1..]);

    private static GivenTypesConjunction TiposDasFixtures() =>
        Types().That().HaveFullNameStartingWith(FixturesFullName);

    // Os nomes dos tipos que a regra reprova. O tipo permitido, pelo nome exato, e os aninhados dele não entram: a conferência olha o que sobra.
    private static string[] Reprovados(IType[] escopo, string? tipoPermitido, MethodMember[] alvos)
    {
        IType[] tipos = tipoPermitido is null
            ? escopo
            : [.. Types().That().Are(escopo).And().DoNotHaveFullName(tipoPermitido).And().DoNotHaveFullNameStartingWith($"{tipoPermitido}+").GetObjects(Arquitetura)];

        return [.. Types().That().Are(tipos).Should().NotCallAny(alvos).Evaluate(Arquitetura)
            .Where(static r => !r.Passed)
            .Select(static r => ((IType)r.EvaluatedObject).FullName)
            .Distinct(StringComparer.Ordinal)];
    }
}
