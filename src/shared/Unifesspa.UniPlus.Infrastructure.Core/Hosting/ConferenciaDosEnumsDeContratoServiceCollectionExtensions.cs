namespace Unifesspa.UniPlus.Infrastructure.Core.Hosting;

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registro da conferência dos códigos de contrato dos enums na subida do executável.</summary>
public static class ConferenciaDosEnumsDeContratoServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="ConferenciaDosEnumsDeContratoNaSubida"/>, que confere os enums de todo projeto do Uni+ que o
    /// executável alcança antes de o host iniciar qualquer serviço. Chamar mais de uma vez não registra uma segunda
    /// conferência.
    /// </summary>
    /// <param name="services">A coleção de serviços do executável.</param>
    /// <param name="executavel">O assembly do ponto de entrada, <c>typeof(Program).Assembly</c>.</param>
    /// <exception cref="ArgumentNullException">A coleção ou o executável é nulo.</exception>
    public static IServiceCollection AddConferenciaDosEnumsDeContrato(this IServiceCollection services, Assembly executavel)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(executavel);

        return services.AddHostedService(_ => new ConferenciaDosEnumsDeContratoNaSubida(executavel));
    }
}
