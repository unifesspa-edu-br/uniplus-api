using System.Reflection;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using ReflectionType = System.Type;

namespace Unifesspa.UniPlus.ArchTests;

/// <summary>
/// Garante que todo código de <c>DomainError</c> emitido em Domain ou Application tem
/// mapeamento em alguma <see cref="IDomainErrorRegistration"/> servida pelo módulo. Sem
/// registro o mapper devolve 500 genérico no lugar do ProblemDetails canônico da
/// ADR-0024, e o contrato de erro do endpoint quebra em silêncio.
/// </summary>
/// <remarks>
/// <para>
/// O código é coletado de duas formas, porque duas convenções convivem no repositório:
/// por reflexão, nas constantes declaradas nos assemblies do módulo, e por varredura
/// dos literais com formato de código no fonte das camadas. A varredura não exige que o
/// literal esteja na construção do <c>DomainError</c> — vários chegam lá por um helper
/// intermediário.
/// </para>
/// <para>
/// Os módulos são descobertos pelo sistema de arquivos, não por um roster no código: um
/// módulo novo passa a ser cobrado sem depender de alguém lembrar de listá-lo aqui.
/// </para>
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

        IReadOnlyList<string> orfaos = [.. emitidos.Except(registrados).Order()];
        orfaos.Should().BeEmpty(
            $"todo code emitido em {modulo}.Domain/{modulo}.Application precisa de mapeamento em "
                + "IDomainErrorRegistration; sem ele o mapper devolve 500 genérico em vez do "
                + $"ProblemDetails canônico (ADR-0024). Sem mapeamento: {string.Join(", ", orfaos)}");
    }

    [Theory(DisplayName = "nenhum code de DomainError é montado por interpolação")]
    [MemberData(nameof(Modulos))]
    public void Codes_NaoSaoMontadosPorInterpolacao(string modulo)
    {
        NenhumCodeMontadoPorInterpolacao(CamadasDeOrigem(modulo));
    }

    [Fact(DisplayName = "codes de DomainError no kernel compartilhado estão registrados")]
    public void Codes_DoKernelCompartilhado_EstaoRegistrados()
    {
        // O kernel fica fora da Theory por não ser módulo de negócio — não tem par
        // Domain/Application nem camada API própria. Mas emite erros que os módulos
        // propagam, e tem registration própria em Infrastructure.Core, então a mesma
        // regra vale: sem mapeamento o mapper devolve 500 genérico.
        string kernel = Path.Join(RaizDoRepositorio(), "src", "shared", "Unifesspa.UniPlus.Kernel");

        HashSet<string> emitidos = LerCodesDeConstantes(Assembly.Load("Unifesspa.UniPlus.Kernel"));
        emitidos.UnionWith(LerCodesEmLiteraisInline([kernel]));

        IReadOnlyList<string> orfaos = [.. emitidos.Except(LerCodesRegistrados("Kernel")).Order()];
        orfaos.Should().BeEmpty(
            "todo code emitido no kernel compartilhado precisa de mapeamento em "
                + "IDomainErrorRegistration; sem ele o mapper devolve 500 genérico em vez do "
                + $"ProblemDetails canônico (ADR-0024). Sem mapeamento: {string.Join(", ", orfaos)}");

        // A recusa da interpolação vale aqui pelo mesmo motivo que vale nos módulos:
        // sem ela um code montado no kernel escaparia às duas coletas e este Fact
        // continuaria verde.
        NenhumCodeMontadoPorInterpolacao([kernel]);
    }

    /// <remarks>
    /// A cobertura por code alcança literal e constante, mas um code montado em tempo
    /// de execução não existe no fonte nem nos metadados: o teste passaria verde sem
    /// cobri-lo. Recusar a construção é o que impede o gate de silenciar.
    /// </remarks>
    private static void NenhumCodeMontadoPorInterpolacao(IEnumerable<string> camadas)
    {
        IEnumerable<string> interpolados = ArquivosDe(camadas)
            .Where(arquivo => ChamadaInterpoladaRegex().IsMatch(SemComentarios(File.ReadAllText(arquivo))))
            .Select(Path.GetFileName)
            .OfType<string>()
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
            .EnumerateDirectories(Path.Join(RaizDoRepositorio(), "src"))
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
            try
            {
                codes.UnionWith(LerCodesDeConstantes(Assembly.Load($"Unifesspa.UniPlus.{modulo}.{camada}")));
            }
            catch (FileNotFoundException)
            {
                // Nem todo módulo tem as duas camadas.
            }
        }

        return codes;
    }

    private static HashSet<string> LerCodesDeConstantes(Assembly assembly)
    {
        {
            // Filtrar por tipo terminado em "ErrorCodes" perderia os codes declarados
            // em classes de constantes com outro nome — ColetabilidadeDeFato e
            // ErrosCodecEnvelope, entre outras, declaram 43 deles. O que identifica
            // um code é o formato do valor, não o nome de quem o declara.
            IEnumerable<string> constantes = TiposDe(assembly)
                .SelectMany(t => t.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (Nome: f.Name, Valor: f.GetRawConstantValue() as string))
                .Where(c => c.Valor is not null && EhCode(c.Nome, c.Valor))
                .Select(c => c.Valor!);

            return [.. constantes];
        }
    }

    /// <remarks>
    /// Um nome de tipo totalmente qualificado tem o mesmo formato de um code
    /// (<c>Npgsql.PostgresException</c>), então o formato sozinho não basta. O que
    /// separa os dois é a convenção de nomeação: a constante de um code se chama
    /// como o sufixo que declara. Vale para os 373 codes do repositório e para
    /// nenhuma das constantes que apenas guardam nome de tipo.
    /// </remarks>
    private static bool EhCode(string nomeDoCampo, string valor)
    {
        if (!FormatoDeCodeRegex().IsMatch(valor))
            return false;

        int ponto = valor.IndexOf('.', StringComparison.Ordinal);
        return string.Equals(valor[(ponto + 1)..], nomeDoCampo, StringComparison.Ordinal);
    }

    private static HashSet<string> LerCodesEmLiteraisInline(string modulo) =>
        LerCodesEmLiteraisInline(CamadasDeOrigem(modulo));

    private static HashSet<string> LerCodesEmLiteraisInline(IEnumerable<string> camadas)
    {
        Regex literal = LiteralDeCodeRegex();
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (string arquivo in ArquivosDe(camadas))
        {
            string conteudo = SemDeclaracaoDeConstante(SemComentarios(File.ReadAllText(arquivo)));

            codes.UnionWith(literal.Matches(conteudo).Select(m => m.Groups[1].Value));
        }

        return codes;
    }

    private static IEnumerable<string> ArquivosDeOrigem(string modulo) => ArquivosDe(CamadasDeOrigem(modulo));

    private static IEnumerable<string> ArquivosDe(IEnumerable<string> camadas) =>
        camadas
            .SelectMany(camada => Directory.EnumerateFiles(camada, "*.cs", SearchOption.AllDirectories))
            .Where(arquivo => !EhArtefatoDeBuild(arquivo));

    /// <remarks>
    /// Remover comentários evita tratar como emitido um code citado em XML doc ou em
    /// linha comentada. Precisão de AST exigiria Roslyn; o strip cobre o que ocorre
    /// no repositório.
    /// </remarks>
    private static string SemComentarios(string conteudo) =>
        ComentarioDeLinhaRegex().Replace(ComentarioEmBlocoRegex().Replace(conteudo, string.Empty), string.Empty);

    /// <remarks>
    /// A declaração de uma constante é território da coleta por reflexão, que sabe
    /// conferir a convenção de nomeação. Deixá-la aqui faria o literal de uma
    /// constante que só guarda nome de tipo entrar como se fosse code.
    /// </remarks>
    private static string SemDeclaracaoDeConstante(string conteudo) =>
        DeclaracaoDeConstanteRegex().Replace(conteudo, string.Empty);

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
        string raiz = Path.Join(RaizDoRepositorio(), "src");

        // OfType descarta a camada ausente: nem todo módulo tem Application própria.
        return ((string[])["Domain", "Application"])
            .Select(camada => Directory
                .EnumerateDirectories(raiz, $"Unifesspa.UniPlus.{modulo}.{camada}", SearchOption.AllDirectories)
                .FirstOrDefault())
            .OfType<string>();
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
        while (atual is not null && !File.Exists(Path.Join(atual, "UniPlus.slnx")))
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
    [GeneratedRegex(@"""([A-Z][A-Za-z0-9]*\.[A-Za-z][A-Za-z0-9]*)""", RegexOptions.Compiled, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LiteralDeCodeRegex();

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*\.[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormatoDeCodeRegex();

    [GeneratedRegex(@"new\s+DomainError\(\s*\$", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChamadaInterpoladaRegex();

    [GeneratedRegex(@"const\s+string\s+\w+\s*=\s*""[^""]*""", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DeclaracaoDeConstanteRegex();

    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.Compiled, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ComentarioEmBlocoRegex();

    [GeneratedRegex(@"//[^\n]*", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ComentarioDeLinhaRegex();
}
