using System.Reflection;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using ReflectionType = System.Type;

namespace Unifesspa.UniPlus.ArchTests;

/// <summary>
/// Garante que todo <c>new DomainError("&lt;code&gt;", ...)</c> escrito em Domain ou
/// Application tem mapeamento em alguma <see cref="IDomainErrorRegistration"/> servida
/// pelo módulo. Sem registro o mapper devolve 500 genérico no lugar do ProblemDetails
/// canônico da ADR-0024, e o contrato de erro do endpoint quebra em silêncio.
/// </summary>
/// <remarks>
/// Os módulos são descobertos pelo sistema de arquivos, não por um roster no código:
/// um módulo novo passa a ser cobrado sem depender de alguém lembrar de listá-lo aqui.
/// </remarks>
public sealed partial class MapeamentoDeDomainErrorTests
{
    public static TheoryData<string> Modulos()
    {
        TheoryData<string> dados = new();
        foreach (string modulo in DescobrirModulos())
            dados.Add(modulo);

        return dados;
    }

    [Theory(DisplayName = "codes de DomainError em Domain/Application estão registrados em IDomainErrorRegistration")]
    [MemberData(nameof(Modulos))]
    public void Codes_DeDomainError_EstaoRegistradosNoMapper(string modulo)
    {
        IReadOnlySet<string> emitidos = LerCodesEmitidos(modulo);
        IReadOnlySet<string> registrados = LerCodesRegistrados(modulo);

        IEnumerable<string> orfaos = emitidos.Except(registrados).Order();
        orfaos.Should().BeEmpty(
            $"todo code emitido em {modulo}.Domain/{modulo}.Application precisa de mapeamento em "
                + "IDomainErrorRegistration; sem ele o mapper devolve 500 genérico em vez do "
                + "ProblemDetails canônico (ADR-0024)");
    }

    [Theory(DisplayName = "nenhum code de DomainError é montado por interpolação")]
    [MemberData(nameof(Modulos))]
    public void Codes_NaoSaoMontadosPorInterpolacao(string modulo)
    {
        // Um code interpolado não é estaticamente determinável, então a coleta acima
        // não o enxerga e a Theory de cobertura passa verde sem cobri-lo. Recusar a
        // construção é o que impede o gate de silenciar: sem isto, um helper
        // `Falha(sufixo)` reintroduz o buraco sem nada acusar.
        IEnumerable<string> interpolados = ArquivosDeOrigem(modulo)
            .Where(arquivo => ChamadaInterpoladaRegex().IsMatch(SemComentarios(File.ReadAllText(arquivo))))
            .Select(arquivo => Path.GetFileName(arquivo))
            .Order();

        interpolados.Should().BeEmpty(
            "o code precisa ser literal ou constante para que a cobertura do registry "
                + "consiga enxergá-lo; monte a mensagem dinamicamente, nunca o code");
    }

    [Fact(DisplayName = "a descoberta de módulos alcança os módulos de negócio do monólito")]
    public void Descoberta_AlcancaOsModulosDeNegocio()
    {
        // Sem esta âncora, um erro de convenção de path faria a Theory rodar com
        // zero casos e o gate passaria vazio, sem cobrir nada.
        DescobrirModulos().Should().Contain(["Selecao", "Configuracao", "Ingresso"]);
    }

    private static IReadOnlyList<string> DescobrirModulos()
    {
        // shared e host não são módulos de negócio: não têm par Domain/Application
        // próprio nem registration de módulo.
        string[] excluidos = ["shared", "host"];

        return [.. Directory
            .EnumerateDirectories(Path.Combine(RaizDoRepositorio(), "src"))
            .Where(pasta => !excluidos.Contains(Path.GetFileName(pasta), StringComparer.Ordinal))
            .SelectMany(pasta => Directory.EnumerateDirectories(pasta, "Unifesspa.UniPlus.*.Domain"))
            .Select(camada => Path.GetFileName(camada)["Unifesspa.UniPlus.".Length..^".Domain".Length])
            .Where(EhCobrado)
            .Order()];
    }

    private static bool EhCobrado(string modulo)
    {
        // Fica de fora o módulo que ainda não expõe endpoint nem declara registration:
        // nenhum code chega ao mapper, e exigir mapeamento obrigaria a inventar o wire
        // code de um endpoint que não existe. Declarar registration própria já basta —
        // quem assumiu o compromisso de mapear é cobrado pela completude dele.
        // O critério se corrige sozinho: no primeiro controller, ou na primeira
        // registration, o módulo volta a ser cobrado.
        Assembly api;
        try
        {
            api = Assembly.Load($"Unifesspa.UniPlus.{modulo}.API");
        }
        catch (FileNotFoundException)
        {
            return false;
        }

        return TiposDe(api).Any(tipo => EhController(tipo) || EhRegistrationDeErros(tipo));
    }

    private static bool EhRegistrationDeErros(ReflectionType tipo) =>
        !tipo.IsAbstract && !tipo.IsInterface && typeof(IDomainErrorRegistration).IsAssignableFrom(tipo);

    private static bool EhController(ReflectionType tipo)
    {
        // Comparação por nome evita depender do assembly do ASP.NET Core aqui e
        // alcança as bases intermediárias de cada módulo.
        for (ReflectionType? atual = tipo.BaseType; atual is not null; atual = atual.BaseType)
        {
            if (atual.FullName == "Microsoft.AspNetCore.Mvc.ControllerBase")
                return true;
        }

        return false;
    }

    private static HashSet<string> LerCodesEmitidos(string modulo)
    {
        // Duas convenções convivem no repositório: módulos com classes `*ErrorCodes`
        // (o code é constante) e módulos que escrevem o literal direto na chamada.
        // Ler só uma das duas deixaria o gate verde sem cobrir nada — Configuracao
        // declara 213 codes por constante e apenas um inline.
        HashSet<string> codes = LerCodesDeclaradosEmConstantes(modulo);
        codes.UnionWith(LerCodesEmLiteraisInline(modulo));
        return codes;
    }

    private static HashSet<string> LerCodesDeclaradosEmConstantes(string modulo)
    {
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (string camada in (string[])["Domain", "Application"])
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.Load($"Unifesspa.UniPlus.{modulo}.{camada}");
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            IEnumerable<FieldInfo> constantes = TiposDe(assembly)
                .Where(t => t.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                .Where(f => f.IsLiteral && f.FieldType == typeof(string));

            foreach (FieldInfo constante in constantes)
            {
                if (constante.GetRawConstantValue() is string code)
                    codes.Add(code);
            }
        }

        return codes;
    }

    private static HashSet<string> LerCodesEmLiteraisInline(string modulo)
    {
        Regex chamada = ChamadaDeDomainErrorRegex();
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (string arquivo in ArquivosDeOrigem(modulo))
        {
            foreach (Match encontro in chamada.Matches(SemComentarios(File.ReadAllText(arquivo))))
                codes.Add(encontro.Groups[1].Value);
        }

        return codes;
    }

    private static IEnumerable<string> ArquivosDeOrigem(string modulo) =>
        CamadasDeOrigem(modulo)
            .SelectMany(camada => Directory.EnumerateFiles(camada, "*.cs", SearchOption.AllDirectories))
            .Where(arquivo => !EhArtefatoDeBuild(arquivo));

    /// <remarks>
    /// Remover comentários evita tratar como emitido um code citado em XML doc ou em
    /// linha comentada. Precisão de AST exigiria Roslyn; o strip cobre o que ocorre
    /// no repositório.
    /// </remarks>
    private static string SemComentarios(string conteudo) =>
        ComentarioDeLinhaRegex().Replace(ComentarioEmBlocoRegex().Replace(conteudo, string.Empty), string.Empty);

    private static HashSet<string> LerCodesRegistrados(string modulo)
    {
        // Whitelist por módulo, não scan amplo: um code registrado apenas em OUTRO
        // módulo não pode contar como cobertura, porque o host daquele módulo não
        // serve a registration alheia. Também exclui stubs de teste, cujos assemblies
        // não casam com o padrão de produção.
        Regex assemblyDeProducao = new(
            @"^Unifesspa\.UniPlus\.(Kernel|Application\.Abstractions|Infrastructure\.Core|"
                + Regex.Escape(modulo) + @"\.(Domain|Application|Infrastructure|API))$",
            RegexOptions.None, TimeSpan.FromSeconds(1));

        CarregarAssembliesDoModulo(modulo);

        HashSet<string> codes = new(StringComparer.Ordinal);
        IEnumerable<ReflectionType> registrations = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name is { } nome && assemblyDeProducao.IsMatch(nome))
            .SelectMany(TiposDe)
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IDomainErrorRegistration).IsAssignableFrom(t));

        foreach (ReflectionType tipo in registrations)
        {
            // Registrations são `internal sealed` com ctor sem parâmetros. Se alguma
            // ganhar dependências de DI, este gate precisa passar a instanciá-la por
            // IServiceProvider — falhar aqui é o aviso de que isso aconteceu.
            ConstructorInfo? ctor = tipo.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                types: []);
            ctor.Should().NotBeNull(
                $"registration {tipo.FullName} precisa de constructor sem parâmetros para ser "
                    + "carregada por este gate");

            IDomainErrorRegistration instancia = (IDomainErrorRegistration)ctor!.Invoke(null);
            foreach (KeyValuePair<string, DomainErrorMapping> mapeamento in instancia.GetMappings())
                codes.Add(mapeamento.Key);
        }

        return codes;
    }

    private static void CarregarAssembliesDoModulo(string modulo)
    {
        // A registration mora na camada API. Assembly.Load é determinístico, ao
        // contrário de tocar um tipo e depender de a JIT já ter materializado a carga.
        _ = typeof(IDomainErrorRegistration).Assembly;

        try
        {
            Assembly.Load($"Unifesspa.UniPlus.{modulo}.API");
        }
        catch (FileNotFoundException)
        {
            // Módulo sem camada API não serve registration própria: os codes que
            // emitir têm de estar cobertos pelas registrations compartilhadas.
        }
    }

    private static IEnumerable<ReflectionType> TiposDe(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<ReflectionType>();
        }
    }

    private static IEnumerable<string> CamadasDeOrigem(string modulo)
    {
        string raiz = Path.Combine(RaizDoRepositorio(), "src");

        foreach (string camada in (string[])["Domain", "Application"])
        {
            string? encontrada = Directory
                .EnumerateDirectories(raiz, $"Unifesspa.UniPlus.{modulo}.{camada}", SearchOption.AllDirectories)
                .FirstOrDefault();

            // Nem todo módulo tem camada Application própria.
            if (encontrada is not null)
                yield return encontrada;
        }
    }

    private static bool EhArtefatoDeBuild(string arquivo)
    {
        char separador = Path.DirectorySeparatorChar;
        return arquivo.Contains($"{separador}bin{separador}", StringComparison.Ordinal)
            || arquivo.Contains($"{separador}obj{separador}", StringComparison.Ordinal);
    }

    private static string RaizDoRepositorio()
    {
        string? atual = AppContext.BaseDirectory;
        while (atual is not null && !File.Exists(Path.Combine(atual, "UniPlus.slnx")))
            atual = Path.GetDirectoryName(atual);

        return atual
            ?? throw new DirectoryNotFoundException(
                "UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");
    }

    /// <remarks>
    /// Complementa a coleta por reflexão, alcançando os módulos que ainda escrevem o
    /// code direto na chamada. Code montado por interpolação ou concatenação escapa a
    /// ambas as coletas — declará-lo como constante ou literal é o que mantém este
    /// gate capaz de enxergá-lo.
    /// </remarks>
    [GeneratedRegex(@"new\s+DomainError\(\s*""([^""]+)""", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChamadaDeDomainErrorRegex();

    [GeneratedRegex(@"new\s+DomainError\(\s*\$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChamadaInterpoladaRegex();

    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.Compiled, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ComentarioEmBlocoRegex();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ComentarioDeLinhaRegex();
}
