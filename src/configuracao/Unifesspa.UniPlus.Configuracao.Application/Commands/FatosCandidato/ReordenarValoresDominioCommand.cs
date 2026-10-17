namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Reordena todos os valores de um fato do administrador, pela lista completa de códigos.</summary>
public sealed record ReordenarValoresDominioCommand(Guid FatoId, IReadOnlyList<string> Codigos) : ICommand<Result>;
