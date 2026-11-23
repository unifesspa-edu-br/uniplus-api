namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta os efeitos na análise do documento.</summary>
public sealed record ListarEfeitosNaAnaliseDoDocumentoQuery : IQuery<IReadOnlyList<CodigoComRotulo<EfeitoNaAnaliseDoDocumento>>>;
