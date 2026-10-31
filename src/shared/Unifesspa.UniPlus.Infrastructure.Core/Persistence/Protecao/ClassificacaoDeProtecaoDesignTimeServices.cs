namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

using System.Reflection;
using System.Runtime.ExceptionServices;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Mantém a classificação de proteção fora do <c>ModelSnapshot</c> das migrations: ela é metadado de
/// proteção do modelo, não schema, e não pode gerar uma anotação por tipo e por coluna em cada snapshot.
/// Todo projeto de startup do <c>dotnet ef</c> a registra num <see cref="IDesignTimeServices"/> próprio:
/// só o <see cref="IDesignTimeServices"/> do assembly de startup roda depois dos serviços do provider,
/// que precisam estar registrados para serem decorados.
/// </summary>
public static class ClassificacaoDeProtecaoDesignTimeServices
{
    /// <summary>
    /// Decora o gerador de anotações do provider para que o snapshot não emita as anotações da
    /// classificação.
    /// </summary>
    public static IServiceCollection ExcluirClassificacaoDoSnapshot(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ServiceDescriptor original = services.LastOrDefault(d => d.ServiceType == typeof(IAnnotationCodeGenerator) && !d.IsKeyedService)
            ?? throw new InvalidOperationException(
                "Nenhum IAnnotationCodeGenerator registrado: os serviços do provider precisam vir antes do filtro da classificação.");

        return services.AddSingleton(provedor => Decorar(provedor, original));
    }

    private static IAnnotationCodeGenerator Decorar(IServiceProvider provedor, ServiceDescriptor original)
    {
        object? geradorDoProvider =
            original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(provedor)
            ?? (original.ImplementationType is null ? null : ActivatorUtilities.CreateInstance(provedor, original.ImplementationType));

        if (geradorDoProvider is not IAnnotationCodeGenerator gerador)
        {
            throw new InvalidOperationException("O registro do IAnnotationCodeGenerator do provider não permite reconstruí-lo.");
        }

        IAnnotationCodeGenerator filtro = DispatchProxy.Create<IAnnotationCodeGenerator, FiltroDeAnotacoes>();
        ((FiltroDeAnotacoes)(object)filtro).Destino = gerador;
        return filtro;
    }

    // Encaminha toda chamada ao gerador do provider e só descarta, em FilterIgnoredAnnotations, as anotações
    // da classificação. DispatchProxy evita reimplementar a interface inteira do EF, que cresce a cada versão.
    // Não é sealed porque o DispatchProxy.Create exige um tipo derivável.
    private class FiltroDeAnotacoes : DispatchProxy
    {
        internal IAnnotationCodeGenerator Destino { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            object? resultado;
            try
            {
                resultado = targetMethod.Invoke(Destino, args);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }

            return targetMethod.Name == nameof(IAnnotationCodeGenerator.FilterIgnoredAnnotations)
                ? ((IEnumerable<IAnnotation>)resultado!).Where(a => !a.Name.StartsWith(ClassificacaoDeProtecaoNoModelo.PrefixoDasAnotacoes, StringComparison.Ordinal))
                : resultado;
        }
    }
}
