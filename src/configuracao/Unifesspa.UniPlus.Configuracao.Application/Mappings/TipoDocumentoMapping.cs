namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class TipoDocumentoMapping
{
    public static TipoDocumentoDto ToDto(this TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoDocumentoDto(
            tipo.Id,
            tipo.Codigo,
            tipo.Nome,
            tipo.Descricao,
            CategoriaDocumentos.ParaTokenCanonico(tipo.Categoria),
            tipo.FormatosAceitos,
            tipo.TamanhoMaximoMb,
            tipo.TipoEquivalente,
            tipo.CreatedAt);
    }
}
