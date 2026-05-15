namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Comando para criar uma <c>AreaOrganizacional</c>. Roteado pelo
/// <c>ICommandBus</c> (Wolverine) ao
/// <c>CriarAreaOrganizacionalCommandHandler</c>. Endpoint admin restrito por
/// policy <c>plataforma-admin</c> (ADR-0057) e <c>Idempotency-Key</c>
/// obrigatório (ADR-0027).
/// </summary>
/// <param name="Codigo">Código da área (validação delegada ao <c>AreaCodigo.From</c>).</param>
/// <param name="Nome">Nome de exibição (2..120 chars).</param>
/// <param name="Tipo">Classificação organizacional.</param>
/// <param name="Descricao">Descrição operacional (1..500 chars).</param>
/// <param name="AdrReferenceCode">Referência à ADR que justifica esta área (closed roster).</param>
public sealed record CriarAreaOrganizacionalCommand(
    string Codigo,
    string Nome,
    TipoAreaOrganizacional Tipo,
    string Descricao,
    string AdrReferenceCode) : ICommand<Result<Guid>>;
