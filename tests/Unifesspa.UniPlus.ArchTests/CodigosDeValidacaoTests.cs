using System.Reflection;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using ReflectionType = System.Type;

namespace Unifesspa.UniPlus.ArchTests;

/// <summary>
/// Garante que toda regra de todo validator FluentValidation das camadas Application publica,
/// em <c>errors[].code</c> do 422, um código registrado no <see cref="IDomainErrorMapper"/> — e
/// não o nome da classe interna do FluentValidation (<c>NotEmptyValidator</c>,
/// <c>MaximumLengthValidator</c>…), que é o que sai quando a regra não declara
/// <c>.WithErrorCode</c> e nenhum resolver global está instalado (ADR-0023).
/// </summary>
/// <remarks>
/// <para>
/// O resolver é instalado pelo mesmo caminho que os hosts usam (<c>AddDomainErrorMapper</c>),
/// e não chamado direto pelo teste: sem a instalação o teste precisa falhar, porque é
/// exatamente essa ausência que publicava o nome da classe no contrato.
/// </para>
/// <para>
/// A varredura desce nos validators filhos (<c>ChildRules</c>, <c>SetValidator</c>,
/// <c>Include</c>) e nas regras dependentes. Sem isso, a regra dentro de um
/// <c>RuleForEach(...).ChildRules(...)</c> — que só existe como instância anônima dentro do
/// adaptador — passaria sem ser olhada.
/// </para>
/// </remarks>
public sealed partial class CodigosDeValidacaoTests
{
    [Fact(DisplayName = "toda regra de validator FluentValidation publica código registrado no mapper")]
    public void TodaRegraDeValidator_PublicaCodigoRegistrado()
    {
        IDomainErrorMapper mapper = MapperDoHost();

        List<string> violacoes = [];
        int regrasVerificadas = 0;

        foreach (ReflectionType tipo in ValidatorsDasCamadasApplication())
        {
            IValidator validator = (IValidator)Activator.CreateInstance(tipo)!;
            foreach ((string regra, string codigo) in CodigosPublicados(validator, tipo.Name, []))
            {
                regrasVerificadas++;

                if (!mapper.TryGetMapping(codigo, out DomainErrorMapping? mapping))
                {
                    violacoes.Add($"{regra}: '{codigo}' não está registrado");
                }
                else if (!CodigoDeWireRegex().IsMatch(mapping.Code))
                {
                    violacoes.Add($"{regra}: '{mapping.Code}' fora da taxonomia da ADR-0023");
                }
            }
        }

        regrasVerificadas.Should().BePositive("a varredura precisa ter encontrado os validators das camadas Application");
        violacoes.Should().BeEmpty(
            "o código de cada regra vira errors[].code no 422; sem registro ele sai cru, com o "
                + "nome da classe interna do FluentValidation. Instale o resolver global "
                + "(ValidationErrorCodes) ou registre o código declarado em .WithErrorCode");
    }

    [Fact(DisplayName = "validator das camadas Application é instanciável sem dependências")]
    public void ValidatorsDasCamadasApplication_TemConstrutorSemParametros()
    {
        // A varredura acima instancia cada validator por reflexão. Um validator com dependência
        // no construtor ficaria de fora em silêncio — recusar é o que mantém a cobertura total.
        IEnumerable<string> semConstrutor = TiposDeValidator()
            .Where(tipo => tipo.GetConstructor(ReflectionType.EmptyTypes) is null)
            .Select(tipo => tipo.FullName!)
            .Order();

        semConstrutor.Should().BeEmpty(
            "o teste de códigos de validação precisa instanciar todo validator; um validator com "
                + "dependência precisa de cobertura própria antes de entrar");
    }

    private static IDomainErrorMapper MapperDoHost()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProblemTypeOptions.SectionName}:BaseUri"] = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/",
            })
            .Build();

        ServiceCollection services = new();
        services.AddDomainErrorMapper(configuration);
        return services.BuildServiceProvider().GetRequiredService<IDomainErrorMapper>();
    }

    private static IEnumerable<ReflectionType> ValidatorsDasCamadasApplication() =>
        TiposDeValidator().Where(tipo => tipo.GetConstructor(ReflectionType.EmptyTypes) is not null);

    private static IEnumerable<ReflectionType> TiposDeValidator() =>
        AssembliesApplication()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(IValidator).IsAssignableFrom(tipo));

    private static IEnumerable<Assembly> AssembliesApplication()
    {
        // Descoberta pelo sistema de arquivos, como MapeamentoDeDomainErrorTests: um módulo novo
        // passa a ser cobrado sem alguém lembrar de listá-lo. Falhar ao carregar é falha do
        // teste, e não omissão silenciosa — a ArchTests referencia o Host, que carrega todos.
        return Directory
            .EnumerateDirectories(Path.Join(RaizDoRepositorio(), "src"))
            .SelectMany(pasta => Directory.EnumerateDirectories(pasta, "Unifesspa.UniPlus.*.Application"))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order()
            .Select(nome => Assembly.Load(nome));
    }

    private static IEnumerable<(string Regra, string Codigo)> CodigosPublicados(
        IValidator validator, string origem, HashSet<IValidator> visitados)
    {
        if (!visitados.Add(validator))
            return [];

        return CodigosPublicados(validator.CreateDescriptor().Rules, origem, visitados);
    }

    private static IEnumerable<(string Regra, string Codigo)> CodigosPublicados(
        IEnumerable<IValidationRule> regras, string origem, HashSet<IValidator> visitados)
    {
        foreach (IValidationRule regra in regras)
        {
            string local = $"{origem}.{regra.PropertyName ?? "(raiz)"}";

            foreach (IRuleComponent componente in regra.Components)
            {
                if (componente.Validator is IChildValidatorAdaptor)
                {
                    IValidator filho = ValidatorFilho(componente.Validator, local);
                    foreach ((string, string) item in CodigosPublicados(filho, local, visitados))
                        yield return item;

                    continue;
                }

                // Mesma precedência que o FluentValidation aplica ao montar a ValidationFailure:
                // o código declarado na regra vence; sem ele, o resolver global.
                string codigo = string.IsNullOrEmpty(componente.ErrorCode)
                    ? ValidatorOptions.Global.ErrorCodeResolver(componente.Validator)
                    : componente.ErrorCode;

                yield return ($"{local} ({componente.Validator.Name})", codigo);
            }

            foreach ((string, string) item in CodigosPublicados(regra.DependentRules ?? [], local, visitados))
                yield return item;
        }
    }

    private static IValidator ValidatorFilho(IPropertyValidator adaptador, string local)
    {
        // O adaptador guarda a instância do validator filho num campo privado. É o único
        // acesso a ela para ChildRules, cujo validator é anônimo (ChildRulesContainer) e não
        // tem tipo próprio a instanciar. SetValidator com fábrica por contexto não é usado no
        // repositório; se aparecer, o campo vem nulo e o teste recusa em vez de pular.
        FieldInfo? campo = null;
        for (ReflectionType? tipo = adaptador.GetType(); tipo is not null && campo is null; tipo = tipo.BaseType)
            campo = tipo.GetField("_validator", BindingFlags.Instance | BindingFlags.NonPublic);

        return campo?.GetValue(adaptador) as IValidator
            ?? throw new InvalidOperationException(
                $"{local}: validator filho não acessível para verificação de códigos "
                    + $"({adaptador.GetType().Name}); cubra-o explicitamente neste teste.");
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

    // Taxonomia de code da ADR-0023.
    [GeneratedRegex(@"^[a-z]+(\.[a-z_]+)+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodigoDeWireRegex();
}
