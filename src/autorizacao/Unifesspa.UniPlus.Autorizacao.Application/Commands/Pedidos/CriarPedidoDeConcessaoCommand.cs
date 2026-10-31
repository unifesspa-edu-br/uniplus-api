namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pede a concessão de um perfil numa unidade (ou na instituição, para o perfil de governança). Qualquer pessoa
/// autenticada pede: pedir não dá acesso, o filtro é a aprovação.
/// </summary>
public sealed record CriarPedidoDeConcessaoCommand(
    string? BeneficiarioEmail,
    Guid PerfilId,
    Guid? UnidadeId,
    string? Justificativa,
    DateTimeOffset? ValidaAteSugerida = null) : ICommand<Result<PedidoDeConcessaoDto>>;
