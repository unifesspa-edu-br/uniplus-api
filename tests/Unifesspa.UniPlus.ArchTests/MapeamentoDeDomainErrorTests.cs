using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using ReflectionType = System.Type;

namespace Unifesspa.UniPlus.ArchTests;

/// <summary>Confere os mapeamentos publicados dos códigos de erro do domínio.</summary>
public sealed class MapeamentoDeDomainErrorTests
{
    public static TheoryData<string> Modulos()
    {
        TheoryData<string> dados = new();
        foreach (string modulo in DescobrirModulos())
            dados.Add(modulo);
        return dados;
    }

    private static IReadOnlyList<string> DescobrirModulos() =>
    [
        .. Directory.EnumerateFiles(AppContext.BaseDirectory, "Unifesspa.UniPlus.*.API.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(static nome => nome is not null)
            .Select(static nome => nome!["Unifesspa.UniPlus.".Length..^".API".Length])
            .Order(StringComparer.Ordinal),
    ];

    private static bool EhAssemblyDeProducao(string nome, string modulo) =>
        nome is "Unifesspa.UniPlus.Kernel" or "Unifesspa.UniPlus.Application.Abstractions" or "Unifesspa.UniPlus.Infrastructure.Core"
        || new[] { "Domain", "Application", "Infrastructure", "API" }
            .Any(camada => nome == $"Unifesspa.UniPlus.{modulo}.{camada}");

    [Theory(DisplayName = "Guard rail da cadeia de versões: corrida responde 409, recusa de corpo responde 422")]
    [InlineData("VersaoConfiguracao.NumeroDuplicado", 409)]
    [InlineData("VersaoConfiguracao.CadeiaQuebrada", 422)]
    [InlineData("VersaoConfiguracao.VigenciaRegressiva", 422)]
    [InlineData("VersaoConfiguracao.AtoCriadorRepetido", 422)]
    [InlineData("VersaoConfiguracao.AtoCriadorJaCriouVersao", 422)]
    [InlineData("VersaoConfiguracao.NumeracaoComBuraco", 422)]
    [InlineData("VersaoConfiguracao.ContratoAberturaInvalido", 422)]
    public void GuardRailDaCadeiaDeVersoes_TemOStatusDaSuaEspecie(string code, int statusEsperado)
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados("Selecao");

        mapeamentos.Should().ContainKey(code);
        mapeamentos[code].Status.Should().Be(statusEsperado);
    }

    /// <summary>
    /// O <c>code</c> permanece específico em todas as sete, inclusive na que passou a 409.
    /// O status responde "dá para tentar de novo?"; o code responde "por quê?". Colapsá-las no
    /// código genérico de conflito apagaria a segunda resposta sem melhorar a primeira, e as
    /// tornaria indistinguíveis do conflito de concorrência otimista, que é outro mecanismo e
    /// tem outro tratamento.
    /// </summary>
    [Fact(DisplayName = "As recusas da cadeia de versões mantêm code próprio, sem colapsar no conflito genérico")]
    public void GuardRailDaCadeiaDeVersoes_NaoColapsaNoCodeGenericoDeConflito()
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados("Selecao");

        IEnumerable<KeyValuePair<string, DomainErrorMapping>> daCadeia = mapeamentos
            .Where(m => m.Key.StartsWith("VersaoConfiguracao.", StringComparison.Ordinal));

        daCadeia.Should().NotBeEmpty();
        daCadeia.Should().OnlyContain(
            m => m.Value.Code != "uniplus.concorrencia.conflito",
            "o code identifica a causa e alimenta o catálogo público de erros; o conflito "
                + "genérico é do middleware de concorrência otimista, e confundir os dois "
                + "esconde qual invariante da cadeia de versões foi violada");
    }

    /// <summary>
    /// A marca de conflito retentável só faz sentido sobre um conflito. O envelope a publica
    /// para qualquer status, mas é um sinal ao cliente: numa recusa de conteúdo ela
    /// prometeria uma repetição que não muda o desfecho.
    /// </summary>
    /// <remarks>
    /// Uma declaração fora de lugar é o primeiro sintoma de que o critério foi aplicado sem o
    /// cenário de corrida na mão.
    /// </remarks>
    [Theory(DisplayName = "só um conflito pode declarar-se retentável")]
    [MemberData(nameof(Modulos))]
    public void RetryableConflict_SoEhDeclaradoEmStatusDeConflito(string modulo)
    {
        Dictionary<string, DomainErrorMapping> mapeamentos = LerMapeamentosRegistrados(modulo);

        IEnumerable<KeyValuePair<string, DomainErrorMapping>> retentaveis = mapeamentos
            .Where(m => m.Value.RetryableConflict);

        retentaveis.Should().OnlyContain(
            m => m.Value.Status == 409,
            "o campo `retryable` do envelope responde \"repetir a mesma requisição pode agora dar "
                + "certo?\", e só o 409 nomeia a corrida que o torna verdadeiro — num 422 ou num 404 "
                + "ele promete ao cliente uma saída que a repetição não oferece");
    }

    private static Dictionary<string, DomainErrorMapping> LerMapeamentosRegistrados(string modulo)
    {
        CarregarAssembliesDoModulo(modulo);

        Dictionary<string, DomainErrorMapping> mapeamentos = new(StringComparer.Ordinal);
        IEnumerable<ReflectionType> registrations = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name is { } nome && EhAssemblyDeProducao(nome, modulo))
            .SelectMany(TiposDe)
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IDomainErrorRegistration).IsAssignableFrom(t));

        foreach (ReflectionType tipo in registrations)
        {
            ConstructorInfo? ctor = tipo.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                types: []);
            if (ctor is null)
            {
                continue;
            }

            IDomainErrorRegistration instancia = (IDomainErrorRegistration)ctor.Invoke(null);
            foreach ((string code, DomainErrorMapping mapeamento) in instancia.GetMappings())
            {
                mapeamentos[code] = mapeamento;
            }
        }

        return mapeamentos;
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
}
