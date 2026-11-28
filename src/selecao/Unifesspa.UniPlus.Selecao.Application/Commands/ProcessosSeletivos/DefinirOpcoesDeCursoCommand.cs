namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Declara quantos cursos o candidato escolhe na inscrição (1 ou 2). A declaração é obrigatória
/// para publicar e depende do número de ofertas da distribuição de vagas. Ausente ou nulo é recusado (422) pelo domínio.
/// </summary>
public sealed record DefinirOpcoesDeCursoCommand(
    Guid ProcessoSeletivoId,
    int? Numero) : ICommand<Result>;
