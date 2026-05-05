namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;
using ReflectionType = System.Type;

/// <summary>
/// Fitness tests do Contrato REST canônico V1 (issue #291). Três regras:
/// <list type="number">
///   <item><description>Cobertura do registry — todo <c>code</c> usado em <c>new DomainError("...", ...)</c>
///   nas camadas Domain e Application está registrado em algum <see cref="IDomainErrorRegistration"/>.</description></item>
///   <item><description>Direção de dependência — Selecao.Domain e Selecao.Application não dependem de
///   <c>Microsoft.AspNetCore.*</c> nem de <c>Selecao.API</c>.</description></item>
///   <item><description>Controllers — tipos em <c>Selecao.API.Controllers</c> não dependem de
///   <see cref="DomainError"/> diretamente; mapeamento é responsabilidade do <see cref="IDomainErrorMapper"/>.</description></item>
/// </list>
/// </summary>
public sealed partial class ContractV1FitnessTestsTests
{
    private static readonly Architecture ModuleArchitecture = LoadModuleArchitecture();

    [Fact(DisplayName = "F1: codes de DomainError em Domain/Application estão registrados em IDomainErrorRegistration")]
    public void Codes_DeDomainError_EstaoRegistradosNoMapper()
    {
        // Coleta source-side: regex sobre os .cs em Domain + Application.
        // Análise estática (não-runtime) é proposital: muitos codes ficam dentro
        // de branches conditionals que nunca executariam num teste sem fixture.
        IReadOnlySet<string> sourceCodes = ScanSourceForDomainErrorCodes(
            FindRepoSourceDir("src/selecao/Unifesspa.UniPlus.Selecao.Domain"),
            FindRepoSourceDir("src/selecao/Unifesspa.UniPlus.Selecao.Application"));

        // Coleta registry-side: instancia todas as IDomainErrorRegistration disponíveis
        // (selecao + cross-cutting) e agrega as keys.
        IReadOnlySet<string> registeredCodes = LoadRegisteredCodes();

        IEnumerable<string> orphans = sourceCodes.Except(registeredCodes);
        orphans.Should().BeEmpty(
            "todo `new DomainError(\"<code>\", ...)` em Domain/Application precisa ter "
                + "mapeamento em IDomainErrorRegistration; sem registry o mapper devolve "
                + "500 genérico em vez do ProblemDetails canônico (ADR-0024).");
    }

    [Fact(DisplayName = "F2: Selecao.Domain e Selecao.Application não dependem de Microsoft.AspNetCore.* nem de Selecao.API")]
    public void DomainAplication_NaoDependemDeAspNetCore()
    {
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .Because("Domain é puro, sem dependência de framework web (ADR-002).");

        IArchRule applicationRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .Because("Application orquestra casos de uso e fala com IBus/IRepo — sem ASP.NET Core.");

        domainRule.Check(ModuleArchitecture);
        applicationRule.Check(ModuleArchitecture);
    }

    [Fact(DisplayName = "F3: Controllers não dependem de DomainError diretamente — mapeamento é via IDomainErrorMapper")]
    public void Controllers_NaoDependemDeDomainErrorDiretamente()
    {
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API\.Controllers(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .Are(typeof(DomainError))
            .Because("Controllers chamam result.ToActionResult(mapper) — não constroem ProblemDetails do "
                + "DomainError manualmente. ADR-0024: mapeamento centralizado preserva taxonomia "
                + "uniplus.<modulo>.<codigo> e status code consistente entre slices.");

        rule.Check(ModuleArchitecture);
    }

    private static Architecture LoadModuleArchitecture()
    {
        // Kernel é incluído explicitamente para que Are(typeof(DomainError)) em
        // F3 resolva contra o IType correspondente no grafo arquitetural —
        // sem o Kernel a regra avaliaria zero dependências e passaria de
        // forma silenciosa.
        ReflectionAssembly[] assemblies =
        [
            typeof(DomainError).Assembly,
            typeof(Domain.Entities.Edital).Assembly,
            typeof(Application.Commands.Editais.CriarEditalCommand).Assembly,
            typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly,
            typeof(API.Controllers.EditalController).Assembly,
        ];

        return new ArchLoader().LoadAssemblies(assemblies).Build();
    }

    private static HashSet<string> ScanSourceForDomainErrorCodes(params string[] roots)
    {
        Regex pattern = DomainErrorCallRegex();
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                string content = File.ReadAllText(file);
                foreach (Match match in pattern.Matches(content))
                    codes.Add(match.Groups[1].Value);
            }
        }

        return codes;
    }

    private static HashSet<string> LoadRegisteredCodes()
    {
        // Varre TODOS os assemblies carregados no AppDomain — uma registration
        // pode acabar em Application, Application.Abstractions, Kernel ou
        // qualquer assembly futuro. Hardcodar dois assemblies geraria falsos
        // orphans quando alguém migrar uma registration para outro lugar.
        // Toca-os primeiro via typeof().Assembly para garantir carga.
        _ = typeof(IDomainErrorRegistration).Assembly;
        _ = typeof(API.Controllers.EditalController).Assembly;

        HashSet<string> codes = new(StringComparer.Ordinal);
        IEnumerable<ReflectionType> registrationTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<ReflectionType>(); }
            })
            .Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(IDomainErrorRegistration).IsAssignableFrom(t));

        foreach (ReflectionType type in registrationTypes)
        {
            ConstructorInfo? ctor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                types: []);
            if (ctor is null)
                continue;

            IDomainErrorRegistration instance = (IDomainErrorRegistration)ctor.Invoke(null);
            foreach (KeyValuePair<string, DomainErrorMapping> mapping in instance.GetMappings())
                codes.Add(mapping.Key);
        }

        return codes;
    }

    private static string FindRepoSourceDir(string relativeFromRepoRoot)
    {
        // Sobe da pasta do binário até achar o slnx; combinar com o path relativo
        // garante portabilidade entre máquinas e CI.
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "UniPlus.slnx")))
            current = Path.GetDirectoryName(current);

        if (current is null)
            throw new DirectoryNotFoundException("UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");

        return Path.Combine(current, relativeFromRepoRoot);
    }

    /// <remarks>
    /// Detecta APENAS literais inline na construção <c>new DomainError("CODE", ...)</c>.
    /// Codes vindos de constantes (<c>const string CodNaoEncontrado = "..."</c>),
    /// interpolação (<c>$"{prefixo}.NaoEncontrado"</c>) ou concatenação escapam
    /// à detecção. Convenção obrigatória do projeto: o code é sempre literal
    /// inline — caso contrário F1 deixa de capturar codes órfãos sem aviso.
    /// </remarks>
    [GeneratedRegex(@"new\s+DomainError\(\s*""([^""]+)""", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DomainErrorCallRegex();
}
