namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
            modalidade.Codigo.EhLegalFixa,
            modalidade.Descricao,
            CodigoComRotulo<NaturezaLegal>.De(modalidade.NaturezaLegal),
            CodigoComRotulo<ComposicaoVagas>.De(modalidade.ComposicaoVagas),
            modalidade.ComposicaoOrigem,
            RemanejamentoDasVagas(modalidade),
            modalidade.RegraDeAplicabilidade.ToDto(),
            modalidade.AcaoQuandoIndeferido is { } acao ? CodigoComRotulo<AcaoQuandoIndeferido>.De(acao) : null,
            modalidade.BaseLegal,
            modalidade.CreatedAt);
    }

    private static RemanejamentoDasVagasDto? RemanejamentoDasVagas(Modalidade modalidade) =>
        modalidade.RegraRemanejamento is { } regra
            ? new(
                CodigoComRotulo<RegraRemanejamento>.De(regra),
                modalidade.RemanejamentoParametros.Destino,
                modalidade.RemanejamentoParametros.Par,
                modalidade.RemanejamentoParametros.DestinoFinal)
            : null;
}
