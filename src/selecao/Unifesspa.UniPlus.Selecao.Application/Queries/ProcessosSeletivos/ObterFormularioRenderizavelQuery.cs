namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Resolve o formulário renderizável de uma finalidade — a apresentação e as regras (UNI-REQ-0144,
/// ADR-0139) — a partir da <c>VersaoConfiguracao</c> que o certame divulgado serve (Story #559, RN08). Projeta sempre dos bytes congelados, nunca da raiz viva: mesmo sob retificação aberta,
/// devolve a versão publicada, não o rascunho em edição — mesmo seletor de
/// <see cref="ObterSnapshotVigenteQuery"/>, resolvido para "agora" (sem instante explícito: é o
/// endpoint público de renderização, não uma consulta forense a um instante passado).
/// </summary>
public sealed record ObterFormularioRenderizavelQuery(
    Guid ProcessoSeletivoId, FinalidadeFormulario Finalidade) : IQuery<Result<FormularioRenderizavelDto>>;
