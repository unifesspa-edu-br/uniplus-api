namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O formulário renderizável da versão que a inscrição do dono do token FIXOU ao iniciar (issue
/// #1859, `GET .../inscricao/formulario`) — nunca a versão vigente por relógio: uma retificação
/// publicada depois não muda o formulário de uma inscrição já em curso (UNI-REQ-0084).
/// </summary>
public sealed record ObterFormularioDaInscricaoQuery(Guid ProcessoSeletivoId) : IQuery<Result<FormularioRenderizavelDto>>;
