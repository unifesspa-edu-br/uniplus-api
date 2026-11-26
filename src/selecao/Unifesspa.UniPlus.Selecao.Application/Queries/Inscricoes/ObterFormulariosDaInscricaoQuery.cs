namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os formulários apresentados da versão em que está a inscrição do dono do token
/// (`GET .../inscricao/formularios`), nunca os da vigente por relógio: no rascunho, a versão divulgada, de
/// modo que uma retificação divulgada muda os formulários; depois do primeiro envio, a congelada, que
/// uma retificação posterior não muda (UNI-REQ-0084).
/// </summary>
public sealed record ObterFormulariosDaInscricaoQuery(Guid ProcessoSeletivoId) : IQuery<Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>>;
