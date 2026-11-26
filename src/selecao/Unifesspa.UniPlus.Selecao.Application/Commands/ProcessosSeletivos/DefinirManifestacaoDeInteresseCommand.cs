namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>Declara como o edital prevê a manifestação de interesse; sem código, a manifestação não é prevista.</summary>
public sealed record DefinirManifestacaoDeInteresseCommand(Guid ProcessoSeletivoId, string? ManifestacaoDeInteresse) : ICommand<Result>;
