namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class RecursoAcessibilidadeMapping
{
    public static RecursoAcessibilidadeDto ToDto(this RecursoAcessibilidade recurso)
    {
        ArgumentNullException.ThrowIfNull(recurso);
        return new RecursoAcessibilidadeDto(
            recurso.Id,
            recurso.Nome,
            recurso.Descricao,
            recurso.CreatedAt);
    }
}
