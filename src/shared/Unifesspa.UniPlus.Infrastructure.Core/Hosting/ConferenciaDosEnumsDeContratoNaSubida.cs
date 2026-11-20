namespace Unifesspa.UniPlus.Infrastructure.Core.Hosting;

using System.Reflection;

using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Confere os códigos de contrato dos enums de todo projeto do Uni+ que o executável alcança, antes de o host iniciar
/// qualquer serviço (ADR-0176). Só adapta a conferência do Kernel ao ciclo de vida do host: o conhecimento de quais
/// assemblies conferir e de o que é defeito fica em <see cref="EnumsDeContrato.ConferirNoExecutavel"/>.
/// </summary>
/// <remarks>
/// O host conclui todos os <c>StartingAsync</c> antes de qualquer <c>StartAsync</c>; por isso o servidor HTTP, os
/// consumidores e o runtime do Wolverine só iniciam depois da conferência, qualquer que seja a ordem de registro. No
/// início sequencial, a primeira falha pode impedir os <c>StartingAsync</c> seguintes; em qualquer modo nenhum
/// <c>StartAsync</c> roda. A falha passa pelo log do próprio host.
/// </remarks>
public sealed class ConferenciaDosEnumsDeContratoNaSubida : IHostedLifecycleService
{
    private readonly Assembly _executavel;

    /// <summary>Cria a conferência do executável informado.</summary>
    /// <param name="executavel">
    /// O assembly do ponto de entrada. Vai por parâmetro porque a construção do host sem iniciá-lo, nos testes, roda o
    /// ponto de entrada dentro do processo do executor de testes, onde o assembly em execução não é o do produto.
    /// </param>
    /// <exception cref="ArgumentNullException">O executável é nulo.</exception>
    public ConferenciaDosEnumsDeContratoNaSubida(Assembly executavel)
    {
        ArgumentNullException.ThrowIfNull(executavel);

        _executavel = executavel;
    }

    /// <exception cref="CodigosDeContratoInvalidosException">Algum enum é inválido; a exceção segue sem embrulho.</exception>
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        EnumsDeContrato.ConferirNoExecutavel(_executavel);

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
