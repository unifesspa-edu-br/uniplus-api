namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Acrescenta valor a um fato de fonte global do administrador.</summary>
public sealed record AdicionarValorDominioCommand(Guid FatoId, string Codigo, string? Descricao, int Ordem) : ICommand<Result>;
