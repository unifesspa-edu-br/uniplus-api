namespace Unifesspa.UniPlus.Autorizacao.Application.Mappings;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>Mapeamento <see cref="VersaoDoTermo"/> para os DTOs do termo. O autor da publicação nunca é exposto.</summary>
public static class TermoMapping
{
    /// <param name="vigenteId">A versão vigente agora; nula se nenhuma foi publicada.</param>
    public static VersaoDoTermoDto ToDto(VersaoDoTermo v, Guid? vigenteId)
    {
        ArgumentNullException.ThrowIfNull(v);

        return new VersaoDoTermoDto(v.Id, v.Numero, v.Texto, v.Resumo, v.Ato, v.PublicadaEm, v.Id == vigenteId);
    }

    public static VersaoDoTermoResumoDto ToResumo(VersaoDoTermo v, Guid? vigenteId)
    {
        ArgumentNullException.ThrowIfNull(v);

        return new VersaoDoTermoResumoDto(v.Id, v.Numero, v.Resumo, v.Ato, v.PublicadaEm, v.Id == vigenteId);
    }
}
