namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Abre a sessão editorial de retificação sobre um certame publicado (ADR-0110 D3).
/// </summary>
/// <remarks>
/// Não recebe precondição: não há sessão a proteger <b>antes</b> de ela existir. A
/// unicidade — só uma sessão por certame — é garantida pelo índice único, não por este
/// contrato.
/// </remarks>
public sealed record AbrirRetificacaoCommand(Guid ProcessoSeletivoId, string Motivo)
    : ICommand<Result<RetificacaoEmCursoDto>>;
