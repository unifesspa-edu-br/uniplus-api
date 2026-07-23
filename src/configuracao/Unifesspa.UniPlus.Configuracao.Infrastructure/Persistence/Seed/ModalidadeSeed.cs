namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Seed do catálogo de <c>modalidade</c>: as oito modalidades federais da Lei 12.711/2012
/// (red. Lei 14.723/2023) mais a ampla concorrência e a modalidade de pessoa com deficiência na
/// ampla concorrência (<c>AC_PCD</c>).
/// </summary>
/// <remarks>
/// <para>
/// As modalidades federais e a ampla concorrência são fato legal fixo, não configurável: a
/// invariante da distribuição de vagas as exige por comparação ordinal quando o edital aplica a
/// regra da Lei 12.711. Sem seed, cada edital as digita à mão, e um erro de grafia só falharia na
/// publicação. Semeá-las por <c>HasData</c> (mesmo mecanismo de <see cref="FatoCandidatoSeed"/>) as
/// torna presentes desde a migração.
/// </para>
/// <para>
/// <c>AC_PCD</c> é a identidade da modalidade de pessoa com deficiência fora da reserva federal; o
/// termo <c>V</c> dos editais vive apenas na <c>Descricao</c>, nunca como código. As suas vagas são
/// <b>retiradas</b> da ampla concorrência (não acrescidas ao total), e a vaga ociosa retorna a
/// <c>AC</c> — daí composição <c>RETIRA_DE</c> origem <c>AC</c> e remanejamento de destino único
/// <c>AC</c>.
/// </para>
/// <para>
/// Consumida tanto pela configuração EF Core (que materializa as linhas via <c>HasData</c>) quanto
/// pelos testes: um confere o seed contra esta lista, outro prova que cada item satisfaz as
/// invariantes de <c>Modalidade.Criar</c>.
/// </para>
/// </remarks>
public static class ModalidadeSeed
{
    private const string BaseLegalLei12711 = "Lei 12.711/2012 (red. Lei 14.723/2023)";

    // Prefixo determinístico próprio do catálogo de modalidades (distinto de fato/valor de domínio).
    private static Guid SeedId(int n) => Guid.Parse($"70da1000-0000-7000-8000-{n:D12}");

    /// <summary>As dez modalidades semeadas, na ordem canônica.</summary>
    public static IReadOnlyList<ModalidadeSeedItem> Itens { get; } =
    [
        new(SeedId(1), "AC", "Ampla concorrência",
            NaturezaLegal.Ampla, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            Regra: null, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(2), "LB_PPI", "Cota — baixa renda, preto/pardo/indígena",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(3), "LB_Q", "Cota — baixa renda, quilombola",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(4), "LB_PCD", "Cota — baixa renda, pessoa com deficiência",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(5), "LB_EP", "Cota — baixa renda, egresso de escola pública",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(6), "LI_PPI", "Cota — independente de renda, preto/pardo/indígena",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(7), "LI_Q", "Cota — independente de renda, quilombola",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(8), "LI_PCD", "Cota — independente de renda, pessoa com deficiência",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(9), "LI_EP", "Cota — independente de renda, egresso de escola pública",
            NaturezaLegal.CotaReservada, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(10), "AC_PCD", "Ampla Concorrência – Pessoa com Deficiência (V)",
            NaturezaLegal.OutraModalidade, ComposicaoVagas.RetiraDe, ComposicaoOrigem: "AC",
            RegraRemanejamento.DestinoUnico, RemanejamentoArgs.Criar("AC", par: null, fallback: null),
            BaseLegalLei12711),
    ];
}

/// <summary>Uma linha do seed de <c>modalidade</c>.</summary>
public sealed record ModalidadeSeedItem(
    Guid Id,
    string Codigo,
    string? Descricao,
    NaturezaLegal Natureza,
    ComposicaoVagas Composicao,
    string? ComposicaoOrigem,
    RegraRemanejamento? Regra,
    RemanejamentoArgs RemanejamentoArgs,
    string? BaseLegal);
