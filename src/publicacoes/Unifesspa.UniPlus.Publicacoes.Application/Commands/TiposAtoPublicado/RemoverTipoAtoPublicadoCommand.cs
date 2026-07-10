namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Remove (soft-delete) uma versão de tipo de ato, liberando a sua janela de
/// vigência para uma nova versão do mesmo código.
/// </summary>
public sealed record RemoverTipoAtoPublicadoCommand(Guid Id) : ICommand<Result>;
