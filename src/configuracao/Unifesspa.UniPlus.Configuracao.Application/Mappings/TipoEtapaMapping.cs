namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

public static class TipoEtapaMapping
{
    public static TipoEtapaDto ToDto(this TipoEtapa tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new TipoEtapaDto(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao, tipo.Ativo,
            new AvaliacaoDasEtapasDto(
                tipo.AdmitePontuacao, tipo.AdmiteEliminacao, tipo.NotaDeOrigemNoEnem, tipo.ExigeCorrecaoIndependente,
                [.. tipo.ConceitosAdmitidos.Select(conceito => new ConceitoAdmitidoDto(
                    conceito.Codigo, conceito.Nome, CodigoComRotulo<FormaDoConceito>.De(conceito.Natureza)))]),
            tipo.CreatedAt);
    }
}
