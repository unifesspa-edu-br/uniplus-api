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
        // Stage1 R3 já banne dependência transitiva Domain/Application → API
        // dentro do mesmo módulo, mas duplicar aqui torna F2 self-contained
        // e o display name fiel ao escopo do teste.
        IArchRule domainRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("Domain é puro, sem dependência de framework web nem da camada de transporte (ADR-002).");

        IArchRule applicationRule = Types()
            .That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.API(\.|$)")
            .Because("Application orquestra casos de uso via IBus/IRepo — sem ASP.NET Core nem tipos da API.");

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
        // Toca os assemblies de produção primeiro para garantir a carga. Sem
        // estes "touches" a JIT pode não materializar os assemblies no AppDomain
        // antes do scan.
        _ = typeof(IDomainErrorRegistration).Assembly; // Infrastructure.Core: kernel + pagination + idempotency
        _ = typeof(API.Controllers.EditalController).Assembly; // Selecao.API: SelecaoDomainErrorRegistration

        // Whitelist explícito de assemblies de produção. Filtrar por convenção
        // de nome em vez de scan amplo evita que stubs de testes (tipo
        // DomainErrorMappingRegistrationStub das unit tests) sejam contados
        // como cobertura — garante que F1 valida APENAS registrations que o
        // host Selecao realmente serve em produção.
        Regex productionAssembly = ProductionAssemblyRegex();

        HashSet<string> codes = new(StringComparer.Ordinal);
        IEnumerable<ReflectionType> registrationTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name is { } name && productionAssembly.IsMatch(name))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<ReflectionType>(); }
            })
            .Where(t => !t.IsAbstract && !t.IsInterface
                && typeof(IDomainErrorRegistration).IsAssignableFrom(t));

        foreach (ReflectionType type in registrationTypes)
        {
            // Suporta ctors internal/private (registrations são `internal sealed`).
            // Se não há ctor default, falha alto: registrations com dependências
            // requerem DI activation real — atualizar F1 antes de adicionar.
            ConstructorInfo? ctor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                types: []);
            ctor.Should().NotBeNull(
                $"registration {type.FullName} precisa de constructor sem parâmetros para ser carregada por F1; "
                    + "se a classe ganhou dependências de DI, atualizar este teste para usar IServiceProvider real.");

            IDomainErrorRegistration instance = (IDomainErrorRegistration)ctor!.Invoke(null);
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

    // Whitelist de assemblies de produção que podem hospedar IDomainErrorRegistration.
    // Mantém a regex restrita: stubs em Unifesspa.UniPlus.*.UnitTests/IntegrationTests/ArchTests
    // não casam (terminam em ".Tests" ou similar) e ficam fora do scan.
    [GeneratedRegex(
        @"^Unifesspa\.UniPlus\.(Kernel|Application\.Abstractions|Infrastructure\.Core|(Selecao|Ingresso)\.(Domain|Application|Infrastructure|API))$",
        RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProductionAssemblyRegex();
}
