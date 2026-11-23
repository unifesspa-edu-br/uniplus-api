namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed record ExportarVersaoPublicadaQuery(Guid ProcessoSeletivoId, int? Versao) : IQuery<Result<VersaoPublicadaExportadaDto>>;
