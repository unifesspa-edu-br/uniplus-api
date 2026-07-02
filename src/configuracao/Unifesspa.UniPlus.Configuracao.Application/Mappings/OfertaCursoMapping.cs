namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class OfertaCursoMapping
{
    public static OfertaCursoDto ToDto(this OfertaCurso oferta)
    {
        ArgumentNullException.ThrowIfNull(oferta);
        return new OfertaCursoDto(
            oferta.Id,
            oferta.CursoId,
            oferta.LocalOfertaId,
            new UnidadeOfertanteDto(
                oferta.UnidadeOfertante.OrigemId,
                oferta.UnidadeOfertante.Sigla,
                oferta.UnidadeOfertante.Nome,
                oferta.UnidadeOfertante.Tipo),
            ProgramasDeOferta.ParaTokenCanonico(oferta.ProgramaDeOferta),
            FormatosPedagogicos.ParaTokenCanonico(oferta.FormatoPedagogico),
            oferta.Turno is { } turno ? TurnosOferta.ParaTokenCanonico(turno) : null,
            oferta.EMecCodigo,
            oferta.CodigoSga,
            oferta.VagasAnuaisAutorizadas,
            oferta.BaseLegal,
            oferta.AtoAutorizacaoMec,
            oferta.CreatedAt);
    }
}
