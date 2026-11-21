namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public static class OfertaCursoMapping
{
    public static OfertaCursoDto ToDto(this OfertaCurso oferta)
    {
        ArgumentNullException.ThrowIfNull(oferta);
        return new OfertaCursoDto(
            oferta.Id, oferta.Codigo, oferta.CursoId, oferta.LocalOfertaId, ParaDto(oferta.UnidadeOfertante),
            CodigoComRotulo<ProgramaDeOferta>.De(oferta.ProgramaDeOferta), CodigoComRotulo<FormatoPedagogico>.De(oferta.FormatoPedagogico),
            CodigoComRotulo<RegimeDeFuncionamento>.De(oferta.RegimeDeFuncionamento), CodigoComRotulo<RegimeDeTurno>.De(oferta.RegimeDeTurno),
            [.. TurnosOferta.OrdenarCanonicamente(oferta.Turnos).Select(CodigoComRotulo<TurnoOferta>.De)],
            oferta.EMecCodigo, oferta.CodigoSga, oferta.VagasAnuaisAutorizadas, oferta.BaseLegal, oferta.AtoAutorizacaoMec, oferta.CreatedAt);
    }

    private static UnidadeOfertanteDto ParaDto(UnidadeOfertante unidade) =>
        new(unidade.OrigemId, unidade.Sigla, unidade.Nome, unidade.Tipo);
}
