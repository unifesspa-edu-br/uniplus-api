namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pede a concessão de um perfil numa unidade (ou na instituição, para o perfil de governança). Qualquer pessoa
/// autenticada pede: pedir não dá acesso, o filtro é a aprovação.
/// </summary>
/// <param name="AnexaraDocumento">O solicitante vai anexar documento; o perfil de governança o exige sempre.</param>
/// <param name="TermoVersaoApresentadaId">
/// A versão do termo de responsabilidade que o cliente apresentou; obrigatória em todo pedido protegido, conferida com a
/// vigente, e proibida no pedido sem termo.
/// </param>
public sealed record CriarPedidoDeConcessaoCommand(
    string? BeneficiarioEmail,
    Guid PerfilId,
    Guid? UnidadeId,
    string? Justificativa,
    DateTimeOffset? ValidaAteSugerida = null,
    bool AnexaraDocumento = false,
    Guid? TermoVersaoApresentadaId = null) : ICommand<Result<PedidoDeConcessaoDto>>;
