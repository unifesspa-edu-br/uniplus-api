namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Aprova uma etapa da aprovação da versão pendente; a permissão da etapa se confere no handler.</summary>
public sealed record AprovarVersaoDoPerfilCommand(Guid PerfilId, Guid VersaoId, string? Etapa) : ICommand<Result<PerfilDto>>;

/// <summary>Recusa uma etapa da aprovação da versão pendente; o motivo é obrigatório.</summary>
public sealed record RecusarVersaoDoPerfilCommand(Guid PerfilId, Guid VersaoId, string? Etapa, string? Motivo) : ICommand<Result<PerfilDto>>;

/// <summary>Cancela a versão pendente, com motivo; quem criou a versão ou quem mantém perfis pode cancelá-la.</summary>
public sealed record CancelarVersaoDoPerfilCommand(Guid PerfilId, Guid VersaoId, string? Motivo) : ICommand<Result<PerfilDto>>;

/// <summary>Retira permissões da versão em vigor do perfil, na hora.</summary>
public sealed record RemoverPermissoesDoPerfilCommand(Guid PerfilId, RemoverPermissoesRequest Dados) : ICommand<Result<PerfilDto>>;

/// <summary>A decisão pedida sobre uma etapa da aprovação, como o handler a entrega ao caso de uso.</summary>
public sealed record DecisaoSolicitada(Guid PerfilId, Guid VersaoId, string? Etapa, ResultadoDaDecisao Resultado, string? Motivo);
