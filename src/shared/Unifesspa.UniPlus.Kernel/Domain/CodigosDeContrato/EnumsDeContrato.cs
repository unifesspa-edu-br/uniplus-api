namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Reflection;
using System.Runtime.ExceptionServices;

/// <summary>
/// Entrada sem tipo genérico para quem só tem o <see cref="Type"/> do enum em mãos, como a conferência na subida e a
/// publicação do contrato.
/// </summary>
public static class EnumsDeContrato
{
    /// <summary>
    /// Confere os enums do executável e de todo assembly que <see cref="AssembliesDoUniPlus.AlcancadosAPartirDe"/>
    /// alcança a partir dele, juntando os defeitos numa só exceção. É a operação que a subida de cada executável chama;
    /// <see cref="ConferirEnumsDe"/> segue sendo a que confere uma lista de assemblies.
    /// </summary>
    /// <remarks>
    /// Os limites do alcance estão em <see cref="AssembliesDoUniPlus"/>. Ler os tipos de cada assembly pode carregar tipos
    /// antes do que a execução carregaria, e a falha de carga sobe sem tratamento.
    /// </remarks>
    /// <exception cref="ArgumentNullException">O executável é nulo.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">Algum enum é inválido.</exception>
    public static void ConferirNoExecutavel(Assembly executavel) =>
        ConferirEnumsDe(AssembliesDoUniPlus.AlcancadosAPartirDe(executavel));

    /// <summary>
    /// Confere todos os enums, em todos os assemblies recebidos, em que algum membro declara
    /// <see cref="CodigoDeContratoAttribute"/>, juntando os defeitos de todos eles numa só exceção. O assembly que
    /// aparece mais de uma vez é conferido uma vez só.
    /// </summary>
    /// <exception cref="CodigosDeContratoInvalidosException">Algum enum é inválido.</exception>
    public static void ConferirEnumsDe(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        List<string> defeitos =
        [
            .. assemblies.Distinct().SelectMany(static a => a.GetTypes()).Where(EhEnumQueDeclaraCodigo).SelectMany(DefeitosDe),
        ];

        if (defeitos.Count > 0)
        {
            throw new CodigosDeContratoInvalidosException(defeitos);
        }
    }

    /// <summary>
    /// Diz se o tipo é um enum em que algum membro declara <see cref="CodigoDeContratoAttribute"/>. O enum em que só
    /// parte dos membros declara também conta: é a conferência que acusa os membros sem código. O tipo anulável de um
    /// enum não é enum: quem varre propriedades desembrulha o tipo antes de perguntar.
    /// </summary>
    public static bool EhEnumQueDeclaraCodigo(Type tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.IsEnum
            && tipo.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Any(static campo => campo.IsDefined(typeof(CodigoDeContratoAttribute), inherit: false));
    }

    /// <summary>Os códigos declarados pelo enum, na ordem em que ele declara os membros.</summary>
    /// <exception cref="ArgumentException">O tipo não é um enum.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static IReadOnlyList<CodigoDeclarado> Declarados(Type tipoDoEnum)
    {
        ArgumentNullException.ThrowIfNull(tipoDoEnum);

        if (!tipoDoEnum.IsEnum)
        {
            throw new ArgumentException($"{tipoDoEnum.Name} não é um enum.", nameof(tipoDoEnum));
        }

        return LerDoCatalogo(tipoDoEnum);
    }

    /// <summary>
    /// A descrição do enum como um todo, ou nulo quando ele não declara <see cref="DescricaoDoContratoAttribute"/>.
    /// Confere o enum antes, como <see cref="Declarados(Type)"/>.
    /// </summary>
    /// <exception cref="ArgumentException">O tipo não é um enum.</exception>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public static string? Descricao(Type tipoDoEnum)
    {
        Declarados(tipoDoEnum);

        return tipoDoEnum.GetCustomAttribute<DescricaoDoContratoAttribute>()?.Descricao;
    }

    private static IReadOnlyList<string> DefeitosDe(Type tipo)
    {
        try
        {
            Declarados(tipo);

            return [];
        }
        catch (CodigosDeContratoInvalidosException excecao)
        {
            return excecao.Defeitos;
        }
    }

    // O catálogo é genérico no enum; a chamada por reflexão devolve a exceção do catálogo, e não a da reflexão.
    private static IReadOnlyList<CodigoDeclarado> LerDoCatalogo(Type tipoDoEnum)
    {
        MethodInfo todos = typeof(CatalogoDe<>).MakeGenericType(tipoDoEnum).GetMethod(nameof(CatalogoDe<>.Todos))
            ?? throw new InvalidOperationException($"O catálogo de {tipoDoEnum.Name} não expõe a lista dos códigos.");

        try
        {
            return todos.Invoke(null, null) as IReadOnlyList<CodigoDeclarado>
                ?? throw new InvalidOperationException($"O catálogo de {tipoDoEnum.Name} devolveu a lista dos códigos em formato inesperado.");
        }
        catch (TargetInvocationException excecao) when (excecao.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(excecao.InnerException).Throw();
            throw;
        }
    }
}
