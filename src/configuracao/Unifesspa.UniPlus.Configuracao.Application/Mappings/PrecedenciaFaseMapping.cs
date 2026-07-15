namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class PrecedenciaFaseMapping
{
    public static PrecedenciaFaseDto ToDto(this PrecedenciaFase aresta)
    {
        ArgumentNullException.ThrowIfNull(aresta);
        return new PrecedenciaFaseDto(
            aresta.Id,
            aresta.AntecessoraCodigo,
            aresta.SucessoraCodigo,
            aresta.PermiteSobreposicao,
            aresta.CreatedAt);
    }
}
