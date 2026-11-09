namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Obtém a janela de migração mais recente do perfil, com a lista de concessões. A permissão se confere no handler.</summary>
public sealed record ObterJanelaDoPerfilQuery(Guid PerfilId) : IQuery<Result<JanelaDeMigracaoDto>>;
