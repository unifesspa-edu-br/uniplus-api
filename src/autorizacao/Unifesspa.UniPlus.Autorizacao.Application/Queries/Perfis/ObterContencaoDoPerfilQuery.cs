namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê a trilha da contenção do perfil: suspensões, decisões das etapas da retomada, desativações e reativações, na
/// ordem em que foram gravadas. Exige manter perfis ou aprovar pela proteção de dados.
/// </summary>
public sealed record ObterContencaoDoPerfilQuery(Guid PerfilId) : IQuery<Result<IReadOnlyList<RegistroDaContencaoDto>>>;
