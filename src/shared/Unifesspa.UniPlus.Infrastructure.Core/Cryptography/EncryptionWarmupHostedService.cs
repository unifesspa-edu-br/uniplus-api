namespace Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

using Microsoft.Extensions.Hosting;

/// <summary>
/// <see cref="IHostedService"/> que força a resolução do singleton
/// <see cref="IUniPlusEncryptionService"/> no startup da aplicação.
///
/// <para>
/// O singleton é registrado com factory lazy em
/// <c>CryptographyServiceCollectionExtensions.AddUniPlusEncryption</c>, então
/// erros do construtor (JWT do ServiceAccount ausente/vazio,
/// <c>KubernetesJwtPath</c> em branco, mutex de auth method) só estourariam no
/// primeiro <c>GetRequiredService&lt;IUniPlusEncryptionService&gt;()</c>. Em
/// produção, hosted services existentes (Idempotency-Key store, cursor
/// pagination) tipicamente forçam a resolução cedo o suficiente para o pod
/// entrar em <c>CrashLoopBackOff</c>, mas isso não é garantido por contrato.
/// Para apps que só tocam cifragem em request, o erro voltaria ao sintoma
/// "500 silencioso" que o <c>EncryptionOptionsValidator</c> ataca parcialmente.
/// </para>
///
/// <para>
/// Este warmup garante <c>CrashLoopBackOff</c> determinístico para qualquer
/// config inválida de cifragem, complementando o validator (que cobre apenas
/// settings que podem ser checadas sem tocar recursos externos).
/// </para>
///
/// <para>
/// Em testes, <c>ApiFactoryBase</c> carrega <c>appsettings.Development.json</c>
/// que já provê <c>UniPlus:Encryption:LocalKey</c> válida, então o warmup roda
/// sem mock. Fixtures que precisem mockar o pipeline sem cifragem real (cenário
/// raro) podem filtrar este hosted service via heurística de
/// <c>ImplementationType</c>, espelhando o pattern de
/// <c>MigrationHostedService&lt;TContext&gt;</c> e <c>WolverineRuntime</c>.
/// </para>
/// </summary>
internal sealed class EncryptionWarmupHostedService : IHostedService
{
    public EncryptionWarmupHostedService(IUniPlusEncryptionService encryptionService)
    {
        // O parâmetro existe para forçar a resolução do singleton via DI durante
        // a enumeração dos IHostedService no Host.StartAsync — qualquer falha do
        // construtor do serviço de cifragem dispara aqui, antes do app aceitar
        // tráfego. ArgumentNullException.ThrowIfNull mantém o contrato explícito.
        ArgumentNullException.ThrowIfNull(encryptionService);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
