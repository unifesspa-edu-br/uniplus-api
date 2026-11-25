namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Resolve o formulário apresentado de uma finalidade — a apresentação e as regras (UNI-REQ-0144,
/// ADR-0139) — a partir da <c>VersaoConfiguracao</c> que o certame divulgado serve (Story #559, congelamento dos parâmetros na publicação, UNI-REQ-0019). Projeta sempre dos bytes congelados, nunca da raiz viva: mesmo sob retificação aberta,
/// devolve a versão publicada, não o rascunho em edição — mesmo seletor de
/// <see cref="ObterSnapshotVigenteQuery"/>, resolvido para "agora" (sem instante explícito: é o
/// endpoint público de apresentação, não uma consulta forense a um instante passado).
/// </summary>
public sealed record ObterFormularioApresentadoQuery(
    Guid ProcessoSeletivoId, FinalidadeFormulario Finalidade) : IQuery<Result<FormularioApresentadoDoProcessoDto>>;
