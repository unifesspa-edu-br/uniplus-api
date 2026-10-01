namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Substitui os itens do formulário de uma finalidade, os fatos que ele coleta do candidato (Story
/// #984); os itens dos outros formulários não mudam. Editável em
/// rascunho (pré-publicação) e sob sessão de retificação de um processo publicado (Story #986). Em
/// rascunho puro a precondição é ignorada (não há sessão nem ETag); sob sessão, o <c>If-Match</c>
/// é obrigatório e a revisão do rascunho avança.
/// </summary>
public sealed record DefinirFatosColetadosCommand(
    Guid ProcessoSeletivoId,
    FinalidadeFormulario Finalidade,
    IReadOnlyList<FatoColetadoInput> Itens,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
