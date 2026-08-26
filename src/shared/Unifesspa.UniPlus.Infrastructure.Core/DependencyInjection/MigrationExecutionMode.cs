namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// Papel do processo quanto às migrations EF Core, escolhido pela chave de
/// configuração <c>UniPlus:Migrations:Mode</c>.
/// </summary>
/// <remarks>
/// Existe porque migration aplicada no boot do pod só é descoberta como quebrada
/// depois que o orquestrador já mexeu nos pods: sob rolling update o pod anterior
/// segue atendendo contra um schema já alterado, e sob recreate não sobra pod
/// nenhum. Separar quem aplica de quem atende permite que o Job de deploy decida
/// se o rollout acontece.
/// </remarks>
public enum MigrationExecutionMode
{
    /// <summary>
    /// Aplica as migrations durante o boot do host. É o default e preserva o
    /// comportamento anterior à separação — ambiente que não declara nada segue
    /// funcionando como sempre.
    /// </summary>
    OnStartup = 0,

    /// <summary>
    /// Aplica as migrations e encerra o processo, sem servir HTTP nem iniciar
    /// mensageria. É o modo do Job de deploy: o código de saída diz ao
    /// orquestrador se o rollout pode seguir.
    /// </summary>
    ApplyAndExit = 1,

    /// <summary>
    /// Não aplica migration alguma. É o modo do pod quando o Job já aplicou —
    /// sem isso o pod reaplicaria por conta própria e a garantia do Job se perderia.
    /// </summary>
    Skip = 2,
}
