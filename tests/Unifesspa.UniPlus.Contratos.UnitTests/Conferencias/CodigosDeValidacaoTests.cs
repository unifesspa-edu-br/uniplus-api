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
using Unifesspa.UniPlus.TestSupport;

using ReflectionType = System.Type;

namespace Unifesspa.UniPlus.Contratos.UnitTests;

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
        (string Regra, string Codigo)[] regras = [.. ValidatorsDasCamadasApplication()
            .SelectMany(tipo => CodigosPublicados((IValidator)Activator.CreateInstance(tipo)!, tipo.Name, []))];
        string[] violacoes = [.. regras.Select(regra => ConferirCodigo(mapper, regra)).OfType<string>()];
        int regrasVerificadas = regras.Length;

        regrasVerificadas.Should().BePositive("a varredura precisa ter encontrado os validators das camadas Application");
        violacoes.Should().BeEmpty(
            "o código de cada regra aparece em errors[].code no 422; sem registro ele sai cru, com o "
                + "nome da classe interna do FluentValidation. Instale o resolver global "
                + "(ValidationErrorCodes) ou registre o código declarado em .WithErrorCode");
    }

    private static string? ConferirCodigo(IDomainErrorMapper mapper, (string Regra, string Codigo) regra)
    {
        if (!mapper.TryGetMapping(regra.Codigo, out DomainErrorMapping? mapping))
            return $"{regra.Regra}: '{regra.Codigo}' não está registrado";
        return CodigoDeWireRegex().IsMatch(mapping.Code) ? null : $"{regra.Regra}: '{mapping.Code}' fora da taxonomia da ADR-0023";
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
        // é cobrado sem alguém precisar listá-lo. Falhar ao carregar é falha do
        // teste, e não omissão silenciosa — o projeto de contratos referencia o Host, que carrega todos.
        return Directory
            .EnumerateDirectories(Path.Join(LocalizadorDaSolucao.Locate(), "src"))
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
        IEnumerable<IValidationRule> regras, string origem, HashSet<IValidator> visitados) =>
        regras.SelectMany(regra => CodigosDaRegra(regra, origem, visitados));

    private static IEnumerable<(string Regra, string Codigo)> CodigosDaRegra(
        IValidationRule regra, string origem, HashSet<IValidator> visitados)
    {
        string local = $"{origem}.{regra.PropertyName ?? "(geral)"}";
        return regra.Components.SelectMany(componente => CodigosDoComponente(componente, local, visitados))
            .Concat(CodigosPublicados(regra.DependentRules ?? [], local, visitados));
    }

    private static IEnumerable<(string Regra, string Codigo)> CodigosDoComponente(
        IRuleComponent componente, string local, HashSet<IValidator> visitados)
    {
        if (componente.Validator is IChildValidatorAdaptor)
            return CodigosPublicados(ValidatorFilho(componente.Validator, local), local, visitados);
        string codigo = string.IsNullOrEmpty(componente.ErrorCode)
            ? ValidatorOptions.Global.ErrorCodeResolver(componente.Validator) : componente.ErrorCode;
        return [($"{local} ({componente.Validator.Name})", codigo)];
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

    // Taxonomia de code da ADR-0023.
    [GeneratedRegex(@"^[a-z]+(\.[a-z_]+)+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodigoDeWireRegex();
}
