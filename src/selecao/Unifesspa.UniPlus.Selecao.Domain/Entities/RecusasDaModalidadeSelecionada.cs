namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Results;

internal static class RecusasDaModalidadeSelecionada
{
    internal static DomainError OrigemObrigatoria() => new(
        "ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio", "Identidade da modalidade de origem é obrigatória.");

    internal static DomainError CodigoObrigatorio() => new(
        "ModalidadeSelecionada.CodigoObrigatorio", "Código da modalidade é obrigatório.");

    internal static DomainError NaturezaLegalObrigatoria(string codigo) => new(
        "ModalidadeSelecionada.NaturezaLegalObrigatoria", $"Natureza legal da modalidade {codigo} é obrigatória.");

    internal static DomainError ComposicaoVagasObrigatoria(string codigo) => new(
        "ModalidadeSelecionada.ComposicaoVagasObrigatoria", $"Composição de vagas da modalidade {codigo} é obrigatória.");

    internal static DomainError BaseLegalObrigatoria(string codigo) => new(
        "ModalidadeSelecionada.BaseLegalObrigatoria", $"Base legal da modalidade {codigo} é obrigatória.");

    internal static DomainError ComposicaoOrigemObrigatoria(string codigo) => new(
        "ModalidadeSelecionada.ComposicaoOrigemObrigatoria", $"Modalidade {codigo} com composição RETIRADA_DE_OUTRA_MODALIDADE exige o código de origem.");

    internal static DomainError ComposicaoOrigemIndevida(string codigo) => new(
        "ModalidadeSelecionada.ComposicaoOrigemIndevida", $"Modalidade {codigo} só declara código de origem quando a composição é RETIRADA_DE_OUTRA_MODALIDADE.");

    internal static DomainError CotaReservadaExigeCascata(string codigo) => new(
        "ModalidadeSelecionada.CotaReservadaExigeCascata", $"Modalidade {codigo} é cota reservada da Lei 12.711 e deve seguir a cascata legal.");

    internal static DomainError RemanejamentoDestinoObrigatorio(string codigo) => new(
        "ModalidadeSelecionada.RemanejamentoDestinoObrigatorio", $"Modalidade {codigo} com remanejamento DESTINO_UNICO exige o destino.");

    internal static DomainError RemanejamentoCruzadoIncompleto(string codigo) => new(
        "ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", $"Modalidade {codigo} com remanejamento CRUZADO exige o par.");

    internal static DomainError RemanejamentoDestinoIndevido(string codigo) => new(
        "ModalidadeSelecionada.RemanejamentoDestinoIndevido", $"Modalidade {codigo} só declara destino de remanejamento quando a regra é DESTINO_UNICO.");

    internal static DomainError RemanejamentoCruzadoIndevido(string codigo) => new(
        "ModalidadeSelecionada.RemanejamentoCruzadoIndevido", $"Modalidade {codigo} só declara par/fallback de remanejamento quando a regra é CRUZADO.");

}
