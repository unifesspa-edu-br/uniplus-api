namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma linha de Pesos por Área: a resolução, o grupo de área, o peso e o corte
/// opcional de cada uma das cinco áreas e a base legal. Os atores de auditoria
/// (<c>created_by</c>) são carimbados server-side via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Resolucao</c>, <c>GrupoCurso</c>, <c>Areas</c> e <c>BaseLegal</c> são anuláveis
/// (ADR-0125): nulo para o campo ausente escapar do <c>[ApiController]</c> e chegar à
/// validação de domínio; sem default, para o schema OpenAPI continuar listando-os como
/// obrigatórios.
/// </remarks>
public sealed record CriarPesoAreaEnemCommand(
    string? Resolucao,
    string? GrupoCurso,
    IReadOnlyList<PesoAreaEnemAreaCommand>? Areas,
    string? BaseLegal) : ICommand<Result<Guid>>;
