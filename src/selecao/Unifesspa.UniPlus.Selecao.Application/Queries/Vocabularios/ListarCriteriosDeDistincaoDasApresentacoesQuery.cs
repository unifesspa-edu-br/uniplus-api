namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Consulta o vocabulário dos critérios que distinguem uma apresentação de outra do mesmo documento.</summary>
public sealed record ListarCriteriosDeDistincaoDasApresentacoesQuery : IQuery<IReadOnlyList<CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>>>;
