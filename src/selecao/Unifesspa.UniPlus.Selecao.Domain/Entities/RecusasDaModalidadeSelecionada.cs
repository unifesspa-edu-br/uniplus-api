namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;

internal static class RecusasDaModalidadeSelecionada
{
    internal static DomainError OrigemObrigatoria() => new(
        "ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio", "Identidade da modalidade de origem é obrigatória.");

    internal static DomainError CodigoObrigatorio() => new(
        "ModalidadeSelecionada.CodigoObrigatorio", "Código da modalidade é obrigatório.");

    internal static DomainError NaturezaLegalObrigatoria() => new(
        "ModalidadeSelecionada.NaturezaLegalObrigatoria", "Natureza legal da modalidade é obrigatória.");

    internal static DomainError ComposicaoVagasObrigatoria() => new(
        "ModalidadeSelecionada.ComposicaoVagasObrigatoria", "Composição de vagas da modalidade é obrigatória.");

    internal static DomainError BaseLegalObrigatoria() => new(
        "ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória.");

    internal static DomainError ComposicaoOrigemObrigatoria() => new(
        "ModalidadeSelecionada.ComposicaoOrigemObrigatoria", "Modalidade com composição RETIRADA_DE_OUTRA_MODALIDADE exige o código da modalidade de origem.");

    internal static DomainError ComposicaoOrigemIndevida() => new(
        "ModalidadeSelecionada.ComposicaoOrigemIndevida", "A modalidade só declara código de origem quando a composição de vagas é RETIRADA_DE_OUTRA_MODALIDADE.");

    internal static DomainError CotaReservadaExigeCascata() => new(
        "ModalidadeSelecionada.CotaReservadaExigeCascata", "A modalidade é cota reservada da Lei 12.711 e deve seguir a ordem legal de remanejamento (SEGUE_ORDEM_DE_REMANEJAMENTO).");

    internal static DomainError RemanejamentoDestinoObrigatorio() => new(
        "ModalidadeSelecionada.RemanejamentoDestinoObrigatorio", "Modalidade com remanejamento DESTINO_UNICO exige o destino.");

    internal static DomainError RemanejamentoCruzadoIncompleto() => new(
        "ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", "Modalidade com remanejamento CRUZADO exige a modalidade par.");

    internal static DomainError RemanejamentoDestinoIndevido() => new(
        "ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO.");

    internal static DomainError RemanejamentoCruzadoIndevido() => new(
        "ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO.");

}
