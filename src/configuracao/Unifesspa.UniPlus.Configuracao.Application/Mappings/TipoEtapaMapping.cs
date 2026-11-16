namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class TipoEtapaMapping
{
    public static TipoEtapaDto ToDto(this TipoEtapa tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoEtapaDto(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao, tipo.Ativo,
            tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem, tipo.ExigeCorrecaoIndependente,
            [.. tipo.ConceitosAdmitidos.Select(conceito => new ConceitoAdmitidoDto(
                conceito.Codigo, conceito.Nome, NaturezasConceito.ParaContrato(conceito.Natureza)))],
            tipo.CreatedAt);
    }
}
