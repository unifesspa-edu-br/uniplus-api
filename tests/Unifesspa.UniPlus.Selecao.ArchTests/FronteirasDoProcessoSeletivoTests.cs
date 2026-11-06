namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// As fronteiras do agregado <see cref="ProcessoSeletivo"/>: as regras vivem em políticas puras que
/// recebem uma vista somente leitura, e só a raiz e os planos que ela aplica mexem nas coleções.
/// </summary>
public sealed class FronteirasDoProcessoSeletivoTests
{
    private const string NamespaceDasPoliticas = "Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos";
    private const string EstadoFullName = NamespaceDasPoliticas + ".Edicao.EstadoDaConfiguracao";
    private const string ColecoesFullName = NamespaceDasPoliticas + ".Edicao.ColecoesDoProcesso";
    private const string PartesFullName = NamespaceDasPoliticas + ".Edicao.PartesDoProcesso";
    private const string ParteFullName = "Unifesspa.UniPlus.Selecao.Domain.Entities.IParteDoProcesso";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(
        typeof(ProcessoSeletivo).Assembly,
        typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
        typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly).Build();

    /// <summary>Os tipos que calculam um plano e o aplicam sobre as coleções da raiz, com os registros do plano.</summary>
    private static readonly string[] Planos =
    [
        "RegrasDoCronograma", "PlanoDoCronograma",
        "AplicacaoDeModeloDeFormulario", "PlanoDaCopiaDoModelo",
        "RegrasDaColeta", "PlanoDaColeta", "PlanoDaRecopiaDoCatalogo",
        "ReposicaoDaConfiguracao",
        "PlanoDaProtecaoVigente",
        "RegrasDasOpcoesDeclaradas",
        "RegrasDosFormularios", "PlanoDoFormulario", "PlanoDosTermos",
        "RegrasDasExigencias", "PlanoDasExigencias",
        "RegrasDaClassificacao", "PlanoDaClassificacao",
    ];

    private static readonly string[] OperacoesDeVinculoEMutacao =
    [
        "VincularProcesso", "VincularProcessoSeletivo", "VincularFinalidade", "Substituir", "AtualizarDados",
        "ReporDe", "AtualizarDe", "RemapearFase", "RemapearAncora", "RemapearDocumentoExigido",
        "RecopiarDoCatalogo", "RecopiarApresentacaoDoCatalogo",
    ];

    [Fact(DisplayName = "A1: nenhuma política depende da raiz, salvo a fábrica da vista")]
    public void A1_PoliticasNaoDependemDaRaiz()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching($@"^{NamespaceDasPoliticas.Replace(".", @"\.", StringComparison.Ordinal)}(\.|$)")
            .And().DoNotHaveFullNameStartingWith(EstadoFullName)
            .Should().NotDependOnAnyTypesThat().HaveFullName(typeof(ProcessoSeletivo).FullName!)
            .Because("a política recebe a vista da configuração; só a fábrica da vista conhece a raiz");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A2: só os planos dependem de ColecoesDoProcesso, e só a raiz o constrói")]
    public void A2_SoOsPlanosTocamAsColecoes()
    {
        string[] dependentes = [.. Arquitetura.Types
            .Where(static t => t.Dependencies.Any(static d => d.Target.FullName == ColecoesFullName))
            .Select(static t => DonoDe(t.FullName))
            .Distinct(StringComparer.Ordinal)
            .Where(static dono => !EhPlanoOuRaiz(dono))
            .Order(StringComparer.Ordinal)];

        dependentes.Should().BeEmpty("a mutação das coleções é da raiz e dos planos que ela aplica");

        string[] construtores = [.. ChamadasNoDomain()
            .Where(static c => c.Callee is ConstructorInfo { DeclaringType.FullName: ColecoesFullName })
            .Select(static c => DonoDe(c.Caller.FullName!))
            .Where(static dono => dono != ColecoesFullName)
            .Distinct(StringComparer.Ordinal)];

        construtores.Should().Equal([typeof(ProcessoSeletivo).FullName], "só a raiz constrói as coleções vivas");
    }

    [Fact(DisplayName = "A3: as classes das políticas são estáticas, salvo os registros")]
    public void A3_PoliticasSaoEstaticas()
    {
        TiposDasPoliticas().Count().Should().BeGreaterThan(30, "a varredura tem de encontrar as políticas");

        string[] naoEstaticas = [.. TiposDasPoliticas()
            .Where(static t => t.IsClass && !(t.IsAbstract && t.IsSealed) && !EhRegistro(t))
            .Select(static t => t.FullName!)];

        naoEstaticas.Should().BeEmpty("a política é função pura da vista; estado vive na raiz");
    }

    [Fact(DisplayName = "A4: fora dos planos, ninguém vincula, substitui, repõe nem altera partes do processo")]
    public void A4_SoOsPlanosVinculamEMutam()
    {
        (string Dono, string Tipo, string Nome)[] controles =
        [
            (NamespaceDasPoliticas + ".Restauracao.ReposicaoDaConfiguracao", PartesFullName, "Substituir"),
            (PartesFullName, ParteFullName, "VincularProcesso"),
            (NamespaceDasPoliticas + ".Exigencias.PlanoDaProtecaoVigente", "Unifesspa.UniPlus.Selecao.Domain.Entities.DocumentoExigido", "RecongelarProtecao"),
        ];
        foreach ((string dono, string tipo, string nome) in controles)
        {
            ChamadasNoDomain()
                .Any(c => DonoDe(c.Caller.FullName!) == dono && c.Callee.DeclaringType!.FullName == tipo && c.Callee.Name == nome)
                .Should().BeTrue($"a leitura do IL tem de enxergar {dono} chamando {tipo}.{nome}, inclusive pela interface");
        }

        MethodInfo mutadorQueDevolveValor = typeof(ProcessoSeletivo).Assembly.GetType("Unifesspa.UniPlus.Selecao.Domain.Entities.RascunhoRetificacao")!
            .GetMethod("AlterarMotivo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        mutadorQueDevolveValor.ReturnType.Should().NotBe(typeof(void), "o controle é um mutador que devolve Result");
        AlteraAParte(mutadorQueDevolveValor).Should().BeTrue("a detecção cobre o mutador com retorno, e não só o void");

        string[] infratores = [.. ChamadasNoDomain()
            .Where(static c => c.Callee.DeclaringType is { } tipo
                && (tipo.FullName is ParteFullName or PartesFullName || tipo.Namespace == typeof(ProcessoSeletivo).Namespace)
                && (EhOperacaoDeVinculoOuMutacao(c.Callee.Name) || AlteraAParte(c.Callee)))
            .Select(static c => DonoDe(c.Caller.FullName!) + " -> " + c.Callee.DeclaringType!.Name + "." + c.Callee.Name)
            .Where(static dono => dono.StartsWith(NamespaceDasPoliticas, StringComparison.Ordinal) && !EhPlanoOuRaiz(dono.Split(" -> ")[0]))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        infratores.Should().BeEmpty("quem altera uma parte do processo, devolvendo valor ou não, é a raiz ou o plano que ela aplica");
    }

    [Fact(DisplayName = "A5: o Domain não depende de Application, Infrastructure, Wolverine nem EF Core")]
    public void A5_DomainNaoDependeDeCamadasExternas()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Application|Infrastructure)(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\.|$)");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A6: toda parte do processo está em Domain.Entities")]
    public void A6_PartesDoProcessoVivemNasEntidades()
    {
        string[] fora = [.. typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.IsClass && t.GetInterfaces().Any(static i => i.FullName == ParteFullName))
            .Where(static t => t.Namespace != typeof(ProcessoSeletivo).Namespace)
            .Select(static t => t.FullName!)];

        fora.Should().BeEmpty("o vínculo com o processo é da entidade filha, não de um tipo de política");
    }

    private static bool AlteraAParte(MethodBase metodo) => LeitorDeIl.EscreveEstado(metodo);

    private static bool EhOperacaoDeVinculoOuMutacao(string nome) =>
        OperacoesDeVinculoEMutacao.Any(op => nome.EndsWith(op, StringComparison.Ordinal));

    private static bool EhPlanoOuRaiz(string dono) =>
        dono == typeof(ProcessoSeletivo).FullName
        || dono is PartesFullName or ColecoesFullName
        || Planos.Contains(dono[(dono.LastIndexOf('.') + 1)..], StringComparer.Ordinal);

    private static bool EhRegistro(System.Type tipo) => tipo.GetMethod("<Clone>$") is not null;

    private static string DonoDe(string nomeCompleto)
    {
        int aninhado = nomeCompleto.IndexOf('+', StringComparison.Ordinal);
        return aninhado < 0 ? nomeCompleto : nomeCompleto[..aninhado];
    }

    private static IEnumerable<System.Type> TiposDasPoliticas() =>
        typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.Namespace is { } ns && ns.StartsWith(NamespaceDasPoliticas, StringComparison.Ordinal))
            .Where(static t => !System.Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)));

    private static IEnumerable<(System.Type Caller, MethodBase Callee)> ChamadasNoDomain() =>
        LeitorDeIl.ChamadasDoAssembly(typeof(ProcessoSeletivo).Assembly);
}
