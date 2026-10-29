namespace Unifesspa.UniPlus.Authorization.Abstractions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Porta pela qual a decisão lê as concessões gravadas de uma pessoa (ADR-0078, ADR-0152). Quem
/// guarda as concessões implementa a porta; o ponto de decisão só a consome, sem conhecer o
/// armazenamento.
/// </summary>
public interface IConcessoesAtivasDoSujeito
{
    /// <summary>
    /// Lista as concessões não revogadas da pessoa, inclusive as de vigência vencida, com as
    /// permissões do perfil de cada uma. Lê o estado atual a cada requisição, sem cache entre
    /// requisições: a revogação vale na requisição seguinte.
    /// </summary>
    Task<IReadOnlyList<ConcessaoAtiva>> ListarAsync(UsuarioRef pessoa, CancellationToken cancellationToken);
}
