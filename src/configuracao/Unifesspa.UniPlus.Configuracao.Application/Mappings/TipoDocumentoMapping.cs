namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public static class TipoDocumentoMapping
{
    public static TipoDocumentoDto ToDto(this TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoDocumentoDto(
            tipo.Id, tipo.Codigo.Valor, tipo.Nome, tipo.Descricao,
            tipo.Categoria,
            tipo.FormatosAceitos,
            tipo.TamanhoMaximoMb,
            tipo.TipoEquivalente,
            CodigoComRotulo<ClassificacaoDeProtecao>.De(tipo.ClassificacaoProtecao),
            tipo.FinalidadeTratamento,
            CodigoComRotulo<HipoteseLegalTratamento>.De(tipo.HipoteseLegal),
            tipo.CreatedAt,
            tipo.Aprovacao.EstaAprovada);
    }
}
