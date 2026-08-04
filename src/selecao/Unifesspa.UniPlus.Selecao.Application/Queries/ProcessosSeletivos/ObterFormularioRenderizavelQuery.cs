namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve o formulário de inscrição — título, termo de aceite e os fatos coletados com sua
/// apresentação — a partir da <c>VersaoConfiguracao</c> vigente do processo (Story #559,
/// RN08). Projeta sempre dos bytes congelados, nunca da raiz viva: mesmo sob retificação aberta,
/// devolve a versão publicada, não o rascunho em edição — mesmo seletor de
/// <see cref="ObterSnapshotVigenteQuery"/>, resolvido para "agora" (sem instante explícito: é o
/// endpoint público de renderização, não uma consulta forense a um instante passado).
/// </summary>
public sealed record ObterFormularioRenderizavelQuery(
    Guid ProcessoSeletivoId) : IQuery<Result<FormularioRenderizavelDto>>;
