namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lista os itens que a versão vigente do edital declara publicar, com a situação de cada um.
/// </summary>
public sealed record ListarProdutosPrevistosDoProcessoQuery(Guid ProcessoSeletivoId)
    : IQuery<Result<ProdutosPrevistosDoProcessoDto>>;
