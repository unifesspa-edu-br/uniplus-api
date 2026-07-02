namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class CursoMapping
{
    public static CursoDto ToDto(this Curso curso)
    {
        ArgumentNullException.ThrowIfNull(curso);
        return new CursoDto(
            curso.Id,
            curso.Codigo,
            curso.Nome,
            curso.Grau,
            curso.NivelEnsino,
            curso.GrupoAreaEnem?.Valor,
            curso.CreatedAt);
    }
}
