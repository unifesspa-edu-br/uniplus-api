namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Edita o nome e a descrição — os eixos do fato não se editam depois do cadastro.</summary>
public sealed record AtualizarFatoCandidatoCommand(Guid Id, string Nome, string? Descricao) : ICommand<Result>;
