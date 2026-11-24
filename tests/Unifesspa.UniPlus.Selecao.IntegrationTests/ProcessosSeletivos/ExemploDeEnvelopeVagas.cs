namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeProcessoRico;

internal static class ExemploDeEnvelopeVagas
{
    internal const int VagasOfertadasBaseDaLeiDeCotasDoExemplo = 60;

    /// <summary>
    /// A matriz legal completa (8×7), fallback AC — mesma forma semeada em
    /// REMANEJ-CASCATA-LEI-12711 v1. <paramref name="permutar"/> inverte a ordem de ENTRADA dos
    /// destinos DENTRO de cada origem — nunca a ordem das origens em si: cada origem aparece uma
    /// única vez na cascata, então permutar a sequência de origens não exercitaria a ordenação de
    /// <c>cascataRemanejamento…destinos</c> — só a dos destinos de uma MESMA origem prova isso.
    /// </summary>
    internal static ConfiguracaoCascataRemanejamento CascataLegalCompleta(bool permutar = false)
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [.. origens.SelectMany(origem => Ordem(DestinosDaOrigem(origem, origens), permutar))];

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, '1'), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    private static List<DestinoRemanejamento> DestinosDaOrigem(string origem, IReadOnlyList<string> origens) =>
        [.. origens.Where(o => o != origem).Select((destino, i) => DestinoRemanejamento.Criar(origem, i + 1, destino).Value!)];

    internal static PredicadoDnf RegraDaCotaIndependenteDeRenda => PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        (0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!),
    ]).Value!;

    internal static PredicadoDnf RegraDaCotaDeRenda => PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (0, CondicaoDnf.Criar("RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!),
        (0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        (0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!),
        (1, CondicaoDnf.Criar("RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!),
        (1, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
    ]).Value!;

    /// <summary>
    /// Oferta sob a Lei 12.711: exige a referência demográfica e as 8 modalidades
    /// federais + AC. É o ramo com <c>referenciaDemografica</c> preenchida. As duas ofertas
    /// do processo rico são deste ramo, porque os cursos de um processo ofertam as mesmas cotas da
    /// lei; as variantes de remanejamento que as cotas não usam ficam na oferta institucional.
    /// </summary>
    internal static ConfiguracaoDistribuicaoVagas DistribuicaoLei12711(
        Guid? ofertaCursoOrigemId = null, (string? Codigo, string? Rotulo)? grupoAreaEnem = null,
        int vagasOfertadasBase = VagasOfertadasBaseDaLeiDeCotasDoExemplo) =>
        ConfiguracaoDistribuicaoVagas.Criar(ofertaCursoOrigemId ?? OfertaMedicina,
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = vagasOfertadasBase,
                PercentualDaReservaLegal = 0.7500m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Lei12711, '7'),
                RegraDeAjuste = Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, '0'),
                ReferenciaDemografica = ReferenciaDemograficaDaLei(),
                Modalidades = [ModalidadeAmplaDaLei(), .. ModalidadesFederais()],
            },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem ?? ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"), OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static ReferenciaReservaDemograficaSnapshot ReferenciaDemograficaDaLei() =>
        ReferenciaReservaDemograficaSnapshot.Criar(
            ReferenciaDemografica,
            censoReferencia: "Censo IBGE 2022",
            ppiPercentual: 78.55m,
            quilombolaPercentual: 1.20m,
            pcdPercentual: 8.40m,
            baseLegal: "Lei 12.711/2012 art. 3º").Value!;

    internal static ModalidadeSelecionada ModalidadeAmplaDaLei() =>
        ModalidadeSelecionada.Criar(
            new Guid("2222bbbb-0000-4000-8000-000000000001"), "AC", "Ampla concorrência",
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012 art. 1º").Value!;

    // As 8 federais. Toda cota reservada obriga SEGUE_CASCATA.
    internal static IEnumerable<ModalidadeSelecionada> ModalidadesFederais() =>
        new[] { "LB_PPI", "LB_Q", "LB_PCD", "LB_EP", "LI_PPI", "LI_Q", "LI_PCD", "LI_EP" }
            .Select(static (codigo, indice) => ModalidadeFederal(codigo, indice + 2));

    /// <summary>
    /// A regra da cota de renda tem duas cláusulas FORA da ordem canônica de propósito: o encoder
    /// ordena as cláusulas e as condições pela chave de conteúdo, então um decoder que preservasse
    /// a ordem de ENTRADA em vez de ler a canônica já gravada produziria bytes distintos dos congelados.
    /// </summary>
    internal static ModalidadeSelecionada ModalidadeFederal(string codigo, int sufixo) =>
        ModalidadeSelecionada.Criar(
            new Guid($"2222bbbb-0000-4000-8000-00000000000{sufixo:x}"), codigo, $"Reserva {codigo}",
            NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal,
            null, RegraRemanejamentoModalidade.SegueCascata, null, null, null,
            codigo.StartsWith("LB", StringComparison.Ordinal) ? RegraDaCotaDeRenda : RegraDaCotaIndependenteDeRenda,
            acaoQuandoIndeferido: "RECLASSIFICAR_AC", baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!;

    /// <summary>
    /// Oferta institucional: quadro fixo, <b>sem</b> referência demográfica — e com as
    /// variantes de remanejamento que a federal não usa (<c>DESTINO_UNICO</c> e
    /// <c>CRUZADO</c>, com par e fallback) e a composição <c>RETIRADA_DE_OUTRA_MODALIDADE</c>.
    /// </summary>
    /// <param name="grupoAreaEnem">
    /// O processo rico classifica pelo ENEM com cálculo local e exige grupo em toda oferta; os
    /// grafos mínimos não, e mantêm a oferta sem grupo.
    /// </param>
    internal static ConfiguracaoDistribuicaoVagas DistribuicaoInstitucional((string? Codigo, string? Rotulo)? grupoAreaEnem = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            OfertaDireito,
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 0.5000m,
                RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, '8'),
                Modalidades = ModalidadesDaOfertaInstitucional(),
            },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ModalidadeSelecionada[] ModalidadesDaOfertaInstitucional() =>
    [
        AmplaDaOfertaInstitucional(),
        ModalidadeSelecionada.Criar(
            new Guid("3333cccc-0000-4000-8000-000000000002"), "V", "PcD em ampla concorrência",
            NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, null,
            RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), "RECLASSIFICAR_AC", "Lei 13.146/2015", quantidadeDeclarada: 5).Value!,
        RetiradaCruzadaDaAmpla(3, "IND", "Indígena", "QUI"),
        RetiradaCruzadaDaAmpla(4, "QUI", "Quilombola", "IND"),
    ];

    private static ModalidadeSelecionada AmplaDaOfertaInstitucional() =>
        ModalidadeSelecionada.Criar(
            new Guid("3333cccc-0000-4000-8000-000000000001"), "AC", null,
            NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
            RegraRemanejamentoModalidade.Nenhuma, null, null, null,
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 15).Value!;

    /// <summary>Ação afirmativa que tira as vagas da ampla concorrência e remaneja em cruz com o par.</summary>
    private static ModalidadeSelecionada RetiradaCruzadaDaAmpla(int sufixo, string codigo, string nome, string par) =>
        ModalidadeSelecionada.Criar(
            new Guid($"3333cccc-0000-4000-8000-00000000000{sufixo}"), codigo, nome,
            NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade, "AC",
            RegraRemanejamentoModalidade.Cruzado, null, par, "AC",
            RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!;
}
