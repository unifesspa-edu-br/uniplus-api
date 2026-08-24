namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta o vocabulário do domínio, sem tocar em repositório: a lista vem de
/// <see cref="FundamentoIsencaoCodigo.Descritos"/>, que por sua vez deriva do enum. Uma
/// segunda lista aqui seria a duplicação que este endpoint existe para evitar no cliente.
/// </summary>
public static class ListarFundamentosIsencaoQueryHandler
{
    public static IReadOnlyList<FundamentoIsencaoDto> Handle(ListarFundamentosIsencaoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return [.. FundamentoIsencaoCodigo.Descritos.Select(static (FundamentoIsencaoDescrito f) =>
            new FundamentoIsencaoDto(f.Codigo, f.Nome, f.Descricao))];
    }
}
