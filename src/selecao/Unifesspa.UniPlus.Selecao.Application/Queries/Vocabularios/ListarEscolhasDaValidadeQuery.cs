namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta as escolhas da validade.</summary>
public sealed record ListarEscolhasDaValidadeQuery : IQuery<IReadOnlyList<CodigoComRotulo<EscolhaDaValidade>>>;
