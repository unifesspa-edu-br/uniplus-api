namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// Seed do catálogo de <c>modalidade</c>: as oito modalidades federais da Lei 12.711/2012
/// (red. Lei 14.723/2023) mais a ampla concorrência, as duas modalidades de pessoa com
/// deficiência fora da reserva federal (<c>AC_PCD</c> e <c>PCD_PURO</c>) e as duas vagas por
/// acréscimo do PSIQ (<c>AC_I</c> e <c>AC_Q</c>).
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
/// As oito cotas federais são <b>dentro das vagas reservadas</b> (<c>DENTRO_DO_VR</c>) — sub-reservas
/// calculadas sobre a fração reservada do total —, e só a ampla concorrência é residual do volume de
/// oferta (<c>RESIDUAL_DO_VO</c>), coerente com o domínio da distribuição de vagas em Seleção.
/// </para>
/// <para>
/// <c>AC_PCD</c> é a identidade da modalidade de pessoa com deficiência fora da reserva federal; o
/// termo <c>V</c> dos editais vive apenas na <c>Descricao</c>, nunca como código. As suas vagas são
/// <b>retiradas</b> da ampla concorrência (não acrescidas ao total), e a vaga ociosa retorna a
/// <c>AC</c> — daí composição <c>RETIRA_DE</c> origem <c>AC</c> e remanejamento de destino único
/// <c>AC</c>. A <b>base legal é institucional</b>, não a Lei de Cotas: a Lei 12.711/2012 não
/// prevê reserva de vaga para pessoa com deficiência fora das suas oito modalidades — <c>AC_PCD</c>
/// existe exatamente para quem essas oito não alcançam.
/// </para>
/// <para>
/// <c>PCD_PURO</c> é a reserva de pessoa com deficiência do processo que <b>não</b> oferta as
/// cotas da Lei 12.711 (UNI-REQ-0085). Ela existe porque <c>AC_PCD</c> não é "a modalidade de
/// PcD": o critério de <c>AC_PCD</c> exclui quem cursou o ensino médio em escola pública, e essa
/// exclusão só se justifica para manter <c>AC_PCD</c> apartada das oito cotas da Lei — num
/// processo sem elas, reaproveitar <c>AC_PCD</c> recusaria indevidamente candidato PcD egresso de
/// escola pública. São cadastros independentes: cada código tem a sua linha, a sua identidade e a
/// sua base legal, e nenhum deriva do outro. A <b>mecânica</b> de vagas, essa sim, é a mesma
/// (<c>RETIRA_DE</c> <c>AC</c>, ociosa volta a <c>AC</c>), porque devolver a vaga não preenchida à
/// ampla concorrência não depende de a modalidade ser exclusiva da Lei.
/// </para>
/// <para>
/// <c>AC_I</c> e <c>AC_Q</c> são as vagas do PSIQ para candidato indígena e quilombola
/// (UNI-REQ-0096). São <b>suplementares ao total</b> — "vagas por acréscimo", no vocabulário da
/// própria universidade: somam-se ao total do curso em vez de disputá-lo, e por isso não têm
/// origem. O remanejamento é <c>CRUZADO</c> e recíproco: a vaga que sobra numa migra para a outra,
/// e vice-versa. Não há <c>fallback</c>, e a ausência é declaração: o PSIQ é certame isolado, sem
/// ampla concorrência no quadro de vagas, então a vaga que nenhum dos dois grupos preenche não tem
/// para onde ir — permanece ociosa.
/// </para>
/// <para>
/// <b>Nenhuma linha declara ação de indeferimento</b> — o campo fica em branco nas treze, e é
/// declaração, não pendência: a modalidade não reclassifica ninguém, e o destino do candidato
/// cuja comprovação é indeferida vem da consequência da exigência documental que o alcançou,
/// declarada no processo. Vale inclusive para as oito cotas da Lei, cuja estrutura é a mais
/// fechada do catálogo.
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

    // A reserva de vaga para pessoa com deficiência é institucional, não da Lei de Cotas: a
    // Lei 12.711/2012 não prevê modalidade de PcD fora das suas oito. Vale para AC_PCD e para
    // PCD_PURO — é a mesma norma sustentando as duas, e citar a Lei em qualquer uma delas
    // fundamentaria a reserva num texto que não a institui (UNI-REQ-0088).
    private const string BaseLegalReservaPcd =
        "Res. Unifesspa 532/2021, art. 1º (reserva de vaga para pessoa com deficiência)";

    // As vagas por acréscimo do PSIQ nascem da Res. 22/2014 e seguem vigentes pela 532/2021 —
    // duas de cada por curso, em todos os cursos (Plano de Trabalho 142/2024-NUADE).
    private const string BaseLegalVagasPorAcrescimo =
        "Res. Unifesspa 22/2014-CONSEPE, atualizada pela Res. Unifesspa 532/2021-CONSEPE "
        + "(vagas por acréscimo para candidatos indígenas e quilombolas)";

    // Prefixo determinístico próprio do catálogo de modalidades (distinto de fato/valor de domínio).
    private static Guid SeedId(int n) => Guid.Parse($"70da1000-0000-7000-8000-{n:D12}");

    /// <summary>As treze modalidades semeadas, na ordem canônica.</summary>
    public static IReadOnlyList<ModalidadeSeedItem> Itens { get; } =
    [
        new(SeedId(1), "AC", "Ampla concorrência",
            NaturezaLegal.Ampla, ComposicaoVagas.ResidualDoVo, ComposicaoOrigem: null,
            Regra: null, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(2), "LB_PPI", "Cota — baixa renda, preto/pardo/indígena",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(3), "LB_Q", "Cota — baixa renda, quilombola",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(4), "LB_PCD", "Cota — baixa renda, pessoa com deficiência",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(5), "LB_EP", "Cota — baixa renda, egresso de escola pública",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(6), "LI_PPI", "Cota — independente de renda, preto/pardo/indígena",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(7), "LI_Q", "Cota — independente de renda, quilombola",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(8), "LI_PCD", "Cota — independente de renda, pessoa com deficiência",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(9), "LI_EP", "Cota — independente de renda, egresso de escola pública",
            NaturezaLegal.CotaReservada, ComposicaoVagas.DentroDoVr, ComposicaoOrigem: null,
            RegraRemanejamento.SegueCascata, RemanejamentoArgs.Vazio, BaseLegalLei12711),

        new(SeedId(10), "AC_PCD", "Ampla Concorrência – Pessoa com Deficiência (V)",
            NaturezaLegal.OutraModalidade, ComposicaoVagas.RetiraDe, ComposicaoOrigem: "AC",
            RegraRemanejamento.DestinoUnico, RemanejamentoArgs.Criar("AC", par: null, fallback: null),
            BaseLegalReservaPcd),

        new(SeedId(11), "PCD_PURO", "Pessoa com Deficiência — reserva sem as cotas da Lei 12.711",
            NaturezaLegal.OutraModalidade, ComposicaoVagas.RetiraDe, ComposicaoOrigem: "AC",
            RegraRemanejamento.DestinoUnico, RemanejamentoArgs.Criar("AC", par: null, fallback: null),
            BaseLegalReservaPcd),

        new(SeedId(12), "AC_I", "Vaga por acréscimo — candidato indígena (PSIQ)",
            NaturezaLegal.Suplementar, ComposicaoVagas.SuplementarAoTotal, ComposicaoOrigem: null,
            RegraRemanejamento.Cruzado, RemanejamentoArgs.Criar(destino: null, par: "AC_Q", fallback: null),
            BaseLegalVagasPorAcrescimo),

        new(SeedId(13), "AC_Q", "Vaga por acréscimo — candidato quilombola (PSIQ)",
            NaturezaLegal.Suplementar, ComposicaoVagas.SuplementarAoTotal, ComposicaoOrigem: null,
            RegraRemanejamento.Cruzado, RemanejamentoArgs.Criar(destino: null, par: "AC_I", fallback: null),
            BaseLegalVagasPorAcrescimo),
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
