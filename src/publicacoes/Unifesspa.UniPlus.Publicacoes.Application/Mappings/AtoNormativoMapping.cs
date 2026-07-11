namespace Unifesspa.UniPlus.Publicacoes.Application.Mappings;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

public static class AtoNormativoMapping
{
    /// <summary>
    /// Projeta o ato em DTO. <c>Avisos</c> fica nulo — a recomputação do aviso de
    /// numeração (AC4) é responsabilidade de quem monta a resposta de detalhe, para
    /// não incorrer em N+1 na listagem.
    /// </summary>
    public static AtoNormativoDto ToDto(this AtoNormativo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        return new AtoNormativoDto(
            ato.Id,
            ato.Orgao,
            ato.Serie,
            ato.Ano,
            ato.Numero,
            ato.TipoCodigo,
            ato.CongelaConfiguracao,
            ato.EfeitoIrreversivel,
            ato.UnicoPorObjeto,
            ato.DataPublicacao,
            ato.DocumentoHash,
            ato.Assinante,
            ato.RegistradoEm,
            ato.VersaoInvocada?.Id,
            ato.VersaoInvocada?.Hash,
            ato.AtoRetificadoId,
            ato.MotivoRetificacao);
    }
}
