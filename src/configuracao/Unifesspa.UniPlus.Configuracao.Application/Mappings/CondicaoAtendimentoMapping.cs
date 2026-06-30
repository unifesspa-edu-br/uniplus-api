namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class CondicaoAtendimentoMapping
{
    public static CondicaoAtendimentoDto ToDto(this CondicaoAtendimentoEspecializado condicao)
    {
        ArgumentNullException.ThrowIfNull(condicao);
        return new CondicaoAtendimentoDto(
            condicao.Id,
            condicao.Codigo.Valor,
            condicao.Nome,
            condicao.Descricao,
            condicao.CreatedAt);
    }
}
