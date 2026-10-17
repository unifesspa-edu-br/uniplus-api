namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Corrige a descrição e a orientação de um valor de fato do administrador.</summary>
public sealed record CorrigirValorDominioCommand(Guid FatoId, string Codigo, string? Descricao, string? Orientacao) : ICommand<Result>;
