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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static partial class CorpusEnvelope
{
    /// <summary>
    /// A matriz legal completa (8×7), fallback AC — mesma forma semeada em
    /// REMANEJ-CASCATA-LEI-12711 v1. <paramref name="permutar"/> inverte a ordem de ENTRADA dos
    /// destinos DENTRO de cada origem — nunca a ordem das origens em si: cada origem aparece uma
    /// única vez na cascata, então permutar a sequência de origens não exercitaria a ordenação de
    /// <c>cascataRemanejamento…destinos</c> — só a dos destinos de uma MESMA origem prova isso.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataLegalCompleta(bool permutar = false)
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            List<DestinoRemanejamento> destinosDestaOrigem = [];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinosDestaOrigem.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }

            destinos.AddRange(Ordem(destinosDestaOrigem, permutar));
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, '1'), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    private static PredicadoDnf RegraDaCotaIndependenteDeRenda => PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        (0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!),
    ]).Value!;

    private static PredicadoDnf RegraDaCotaDeRenda => PredicadoDnf.CriarDeCondicoesAgrupadas(
    [
        (0, CondicaoDnf.Criar("RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!),
        (0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        (0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!),
        (1, CondicaoDnf.Criar("RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!),
        (1, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
    ]).Value!;

    /// <summary>
    /// Oferta sob a Lei 12.711: exige a referência demográfica (INV-5) e as 8 modalidades
    /// federais + AC (INV-6). É o ramo com <c>referenciaDemografica</c> preenchida. As duas ofertas
    /// do processo rico são deste ramo, porque os cursos de um processo ofertam as mesmas cotas da
    /// lei; as variantes de remanejamento que as cotas não usam ficam na oferta institucional.
    /// </summary>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoLei12711(
        Guid? ofertaCursoOrigemId = null, (string? Codigo, string? Rotulo)? grupoAreaEnem = null)
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeSelecionada.Criar(
                new Guid("2222bbbb-0000-4000-8000-000000000001"), "AC", "Ampla concorrência",
                NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null,
                baseLegal: "Lei 12.711/2012 art. 1º").Value!,
        ];

        // As 8 federais. Todas cota reservada ⇒ INV-12 obriga SEGUE_CASCATA.
        (string Codigo, char Semente)[] federais =
        [
            ("LB_PPI", 'g'), ("LB_Q", 'h'), ("LB_PCD", 'i'), ("LB_EP", 'j'),
            ("LI_PPI", 'k'), ("LI_Q", 'l'), ("LI_PCD", 'm'), ("LI_EP", 'n'),
        ];

        int sufixo = 2;
        foreach ((string codigo, char _) in federais)
        {
            modalidades.Add(ModalidadeSelecionada.Criar(
                new Guid($"2222bbbb-0000-4000-8000-00000000000{sufixo:x}"),
                codigo,
                $"Reserva {codigo}",
                NaturezaLegalModalidade.CotaReservada,
                ComposicaoVagasModalidade.DentroDoVr,
                null,
                RegraRemanejamentoModalidade.SegueCascata,
                null,
                null,
                null,
                // A regra da cota de renda tem duas cláusulas FORA da ordem canônica de propósito: o
                // encoder ordena as cláusulas e as condições pela chave de conteúdo, então um decoder
                // que preservasse a ordem de ENTRADA em vez de ler a canônica já gravada produziria
                // bytes distintos dos congelados.
                codigo.StartsWith("LB", StringComparison.Ordinal) ? RegraDaCotaDeRenda : RegraDaCotaIndependenteDeRenda,
                acaoQuandoIndeferido: "RECLASSIFICAR_AC",
                baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!);
            sufixo++;
        }

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: ofertaCursoOrigemId ?? OfertaMedicina,
            voBase: 60,
            pr: 0.7500m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Lei12711, '7'),
            regraAjuste: Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, '0'),
            referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(
                ReferenciaDemografica,
                censoReferencia: "Censo IBGE 2022",
                ppiPercentual: 78.55m,
                quilombolaPercentual: 1.20m,
                pcdPercentual: 8.40m,
                baseLegal: "Lei 12.711/2012 art. 3º").Value!,
            modalidades: modalidades,
            grupoAreaEnem: grupoAreaEnem ?? ("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"), ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
    }

    /// <summary>
    /// Oferta institucional: quadro fixo, <b>sem</b> referência demográfica — e com as
    /// variantes de remanejamento que a federal não usa (<c>DESTINO_UNICO</c> e
    /// <c>CRUZADO</c>, com par e fallback) e a composição <c>RETIRA_DE</c>.
    /// </summary>
    /// <param name="grupoAreaEnem">
    /// O processo rico classifica pelo ENEM com cálculo local e exige grupo em toda oferta; os
    /// grafos mínimos não, e mantêm a oferta sem grupo.
    /// </param>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoInstitucional((string? Codigo, string? Rotulo)? grupoAreaEnem = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaDireito,
            voBase: 40,
            pr: 0.5000m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, '8'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 15).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000002"), "V", "PcD em ampla concorrência",
                    NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.SuplementarAoTotal, null,
                    RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), "RECLASSIFICAR_AC", "Lei 13.146/2015", quantidadeDeclarada: 5).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000003"), "IND", "Indígena",
                    NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "QUI", "AC",
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000004"), "QUI", "Quilombola",
                    NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "IND", "AC",
                    RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.AcaoAfirmativa), "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
            ],
            grupoAreaEnem: grupoAreaEnem, ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
}
