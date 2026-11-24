namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta os códigos e rótulos de TipoDoDado.</summary>
public sealed record ListarTiposDoDadoQuery : IQuery<IReadOnlyList<CodigoComRotulo<TipoDoDado>>>;
