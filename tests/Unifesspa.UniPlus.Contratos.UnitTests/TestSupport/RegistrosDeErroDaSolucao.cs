namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.Reflection;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Os registros de erro de todos os assemblies de produto, instanciados em memória. A descoberta
/// é por tipo, e não por lista: um registro novo entra no catálogo de códigos sem que alguém o
/// lembre de acrescentar a um roster.
/// </summary>
internal static class RegistrosDeErroDaSolucao
{
    private const BindingFlags ConstrutorDeQualquerVisibilidade = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static IReadOnlyList<IDomainErrorRegistration> Instanciar() =>
    [
        .. AssembliesDoProduto.Todos
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(EhRegistroConcreto)
            .Select(Criar),
    ];

    private static bool EhRegistroConcreto(Type tipo) =>
        tipo is { IsAbstract: false, IsInterface: false } && typeof(IDomainErrorRegistration).IsAssignableFrom(tipo);

    private static IDomainErrorRegistration Criar(Type tipo)
    {
        ConstructorInfo construtor = tipo.GetConstructor(ConstrutorDeQualquerVisibilidade, Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"{tipo.FullName} implementa IDomainErrorRegistration sem construtor sem parâmetros; "
                + "o catálogo de códigos de erro precisa instanciá-la para listar os códigos.");

        return (IDomainErrorRegistration)construtor.Invoke(null);
    }
}
