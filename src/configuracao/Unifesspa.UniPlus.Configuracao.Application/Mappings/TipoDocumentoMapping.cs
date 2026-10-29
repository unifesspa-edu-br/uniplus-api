namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public static class TipoDocumentoMapping
{
    public static TipoDocumentoDto ToDto(this TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoDocumentoDto(
            tipo.Id,
            tipo.Codigo.Valor,
            tipo.Nome,
            tipo.Descricao,
            tipo.Categoria,
            tipo.FormatosAceitos,
            tipo.TamanhoMaximoMb,
            tipo.TipoEquivalente,
            ClassificacoesDeProtecao.ParaTokenCanonico(tipo.ClassificacaoProtecao),
            tipo.FinalidadeTratamento,
            HipotesesLegaisTratamento.ParaTokenCanonico(tipo.HipoteseLegal),
            tipo.CreatedAt);
    }
}
