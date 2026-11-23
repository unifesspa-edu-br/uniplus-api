namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta os tipos da data de referência.</summary>
public sealed record ListarTiposDaDataDeReferenciaQuery : IQuery<IReadOnlyList<CodigoComRotulo<TipoDaDataDeReferencia>>>;
