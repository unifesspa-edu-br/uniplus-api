namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class ModalidadeMapping
{
    public static ModalidadeDto ToDto(this Modalidade modalidade)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        return new ModalidadeDto(
            modalidade.Id,
            modalidade.Codigo.Valor,
            modalidade.Descricao,
            NaturezasLegais.ParaTokenCanonico(modalidade.NaturezaLegal),
            ComposicoesVagas.ParaTokenCanonico(modalidade.ComposicaoVagas),
            modalidade.ComposicaoOrigem,
            modalidade.RegraRemanejamento is { } regra
                ? RegrasRemanejamento.ParaTokenCanonico(regra)
                : null,
            modalidade.RemanejamentoArgs.Destino,
            modalidade.RemanejamentoArgs.Par,
            modalidade.RemanejamentoArgs.Fallback,
            modalidade.CriteriosCumulativos,
            modalidade.AcaoQuandoIndeferido is { } acao
                ? AcoesQuandoIndeferido.ParaTokenCanonico(acao)
                : null,
            modalidade.BaseLegal,
            modalidade.CreatedAt);
    }
}
