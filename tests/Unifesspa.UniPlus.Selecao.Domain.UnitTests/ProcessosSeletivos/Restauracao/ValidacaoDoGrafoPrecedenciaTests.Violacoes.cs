namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ProcessoSeletivoRestaurarConfiguracaoTests;

public sealed partial class ValidacaoDoGrafoPrecedenciaTests
{
    /// <summary>As violações do grafo restaurado, na ordem em que a validação as confere.</summary>
    public enum Violacao
    {
        IdDeEtapaAusente,
        IdDeEtapaDuplicado,
        OrdemDeEtapaDuplicada,
        EtapaDoEnemSobClassificacaoSemEnem,
        NenhumaEtapaComponeANota,
        DistribuicaoDeVagasVazia,
        OfertaDeCursoDuplicada,
        OpcoesDeCursoForaDaFaixa,
        OpcoesDeCursoAcimaDasOfertas,
        AcaoQuandoIndeferidoDivergente,
        RegraDeAplicabilidadeDivergente,
        RegraIncoerenteComANatureza,
        RemanejamentoIncoerenteComANatureza,
        CriteriosDeDesempateAcimaDoTeto,
        ItensDoFormularioAcimaDoTeto,
        OrdemDeDesempateDuplicada,
        DesempateCitaEtapaInexistente,
        DesempatePorAreaSemQuadro,
        EliminacaoCitaEtapaInexistente,
        AusenciaNaEtapaDoEnem,
        CronogramaVazio,
        OrdemDeFaseDuplicada,
        FaseCanonicaDuplicada,
        FaseAgrupadoraSemEtapa,
        EtapaSemFaseAlguma,
        ArvoreDeExigenciasInconsistente,
    }

    private sealed record Recusa(string Codigo, string Mensagem, Action<GrafoEmMontagem> Provoca);

    private static readonly Dictionary<Violacao, Recusa> Recusas = new()
    {
        [Violacao.IdDeEtapaAusente] = new(
            "ProcessoSeletivo.IdEtapaAusente",
            "Toda etapa restaurada deve declarar o Id congelado no envelope.",
            g => g.EtapasAdicionais.Add(EtapaSemId(10))),
        [Violacao.IdDeEtapaDuplicado] = new(
            "ProcessoSeletivo.IdEtapaDuplicado",
            "O mesmo Id de etapa não pode aparecer mais de uma vez na configuração restaurada.",
            g => g.EtapasAdicionais.AddRange([EtapaDaProvaCongelada(Id(20), 20), EtapaDaProvaCongelada(Id(20), 21)])),
        [Violacao.OrdemDeEtapaDuplicada] = new(
            "ProcessoSeletivo.OrdemEtapaDuplicada",
            "Cada etapa deve ter uma ordem única dentro do processo.",
            g => g.EtapasAdicionais.AddRange([EtapaDaProvaCongelada(Id(30), 30), EtapaDaProvaCongelada(Id(31), 30)])),
        [Violacao.EtapaDoEnemSobClassificacaoSemEnem] = new(
            "ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem",
            "A etapa \"Nota do ENEM\" tem a nota vinda do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada.",
            g => g.EtapasAdicionais.Add(EtapaDoEnemSemPeso(Id(40), 40))),
        [Violacao.NenhumaEtapaComponeANota] = new(
            "ProcessoSeletivo.NenhumaEtapaComponeNota",
            "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.",
            g => g.ProvaCompoeANota = false),
        [Violacao.DistribuicaoDeVagasVazia] = new(
            "ProcessoSeletivo.DistribuicaoVagasVazia",
            "O processo deve ter ao menos uma distribuição de vagas configurada.",
            g => g.SemDistribuicao = true),
        [Violacao.OfertaDeCursoDuplicada] = new(
            "ProcessoSeletivo.OfertaCursoDuplicada",
            "Cada oferta de curso só pode ter uma distribuição de vagas no processo.",
            g => g.OfertasAdicionais.Add(Oferta(Id(100)))),
        [Violacao.OpcoesDeCursoForaDaFaixa] = new(
            "ProcessoSeletivo.OpcoesDeCursoInvalido",
            "O número de opções de curso da configuração restaurada deve ser 1 ou 2.",
            g => g.OpcoesDeCurso = 3),
        [Violacao.OpcoesDeCursoAcimaDasOfertas] = new(
            "ProcessoSeletivo.OpcoesDeCursoAcimaDasOfertas",
            "Com 2 opções de curso, cadastre pelo menos dois cursos.",
            g => g.OpcoesDeCurso = Math.Max(g.OpcoesDeCurso ?? 0, 2)),
        [Violacao.AcaoQuandoIndeferidoDivergente] = new(
            "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente",
            "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo.",
            g => g.OfertasAdicionais.AddRange(
                [Oferta(Id(110), null, AcaoAfirmativaComDestino("RECLASSIFICAR_AC")), Oferta(Id(111), null, AcaoAfirmativaComDestino("RECLASSIFICAR_REGRA_EDITAL"))])),
        [Violacao.RegraDeAplicabilidadeDivergente] = new(
            "ProcessoSeletivo.RegraDeAplicabilidadeDivergente",
            "A modalidade AC foi copiada com regras de aplicabilidade diferentes em ofertas do processo; grave a distribuição de vagas de novo.",
            g => g.OfertasAdicionais.Add(Oferta(Id(120), RegraDeAplicabilidadeDivergenteDaAmpla()))),
        [Violacao.RegraIncoerenteComANatureza] = new(
            "ProcessoSeletivo.RegraIncoerenteComANatureza",
            "A regra de aplicabilidade da modalidade LI_EP não tem a forma que a natureza dela pede; grave a distribuição de vagas de novo para copiar a regra do cadastro.",
            g => g.ModalidadesDaOfertaBase.Add(CotaDaLeiSemAOpcaoPelasCotas())),
        [Violacao.RemanejamentoIncoerenteComANatureza] = new(
            "ProcessoSeletivo.RemanejamentoIncoerenteComANatureza",
            "A regra de remanejamento da modalidade AC_PCD não é a que a natureza dela pede; grave a distribuição de vagas de novo para copiar o remanejamento do cadastro.",
            g => g.ModalidadesDaOfertaBase.Add(AcaoAfirmativaSemRemanejamento())),
        [Violacao.CriteriosDeDesempateAcimaDoTeto] = new(
            "ProcessoSeletivo.CriteriosDesempateEmExcesso",
            "A ordem de desempate admite no máximo 20 critérios; vieram 21.",
            g => g.Criterios.AddRange(Enumerable.Range(101, RegrasDoDesempate.CriteriosDesempateMaximo + 1).Select(DesempatePorIdade))),
        [Violacao.ItensDoFormularioAcimaDoTeto] = new(
            "ItemFormulario.ItensEmExcesso",
            "O formulário admite no máximo 200 itens; vieram 201.",
            g => g.Fatos.AddRange(Enumerable.Range(0, FormaDoItem.MaximoDeItens + 1).Select(FatoDoFormulario))),
        [Violacao.OrdemDeDesempateDuplicada] = new(
            "ProcessoSeletivo.OrdemDesempateDuplicada",
            "Cada critério de desempate deve ter uma ordem única dentro do processo.",
            g => g.Criterios.AddRange([DesempatePorIdade(1), DesempatePorIdade(1)])),
        [Violacao.DesempateCitaEtapaInexistente] = new(
            "ProcessoSeletivo.EtapaRefDesempateInexistente",
            $"O critério de desempate na ordem 2 referencia a etapa {Id(300)}, que não existe na configuração restaurada (INV-B6).",
            g => g.Criterios.Add(DesempatePelaNotaDaEtapa(2, Id(300)))),
        [Violacao.DesempatePorAreaSemQuadro] = new(
            "ProcessoSeletivo.DesempateAreaEnemSemQuadro",
            "O critério de desempate na ordem 3 compara a nota por área do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada, com o quadro de pesos por área congelado.",
            g => g.Criterios.Add(DesempatePorArea(3, "REDACAO"))),
        [Violacao.EliminacaoCitaEtapaInexistente] = new(
            "ProcessoSeletivo.EtapaRefEliminacaoInexistente",
            $"A regra de eliminação referencia a etapa {Id(310)}, que não existe na configuração restaurada (INV-B4).",
            g => g.Eliminacoes.Add(EliminacaoPorAusenciaNaEtapa(Id(310)))),
        [Violacao.AusenciaNaEtapaDoEnem] = new(
            "ProcessoSeletivo.AusenciaNaEtapaDoEnem",
            "A eliminação por ausência não se aplica à etapa de nota do ENEM, cuja falta é tratada pela eliminação por falta em dia de prova do ENEM.",
            ComEtapaDoEnemEliminadaPorAusencia),
        [Violacao.CronogramaVazio] = new(
            "ProcessoSeletivo.CronogramaFasesVazio",
            "O processo deve ter ao menos uma fase no cronograma.",
            g => g.SemCronograma = true),
        [Violacao.OrdemDeFaseDuplicada] = new(
            "ProcessoSeletivo.OrdemFaseDuplicada",
            "Cada fase deve ter uma ordem única dentro do cronograma.",
            g => g.FasesAdicionais.Add(Fase(1, Id(210), agrupaEtapas: false))),
        [Violacao.FaseCanonicaDuplicada] = new(
            "ProcessoSeletivo.FaseCanonicaDuplicada",
            "A mesma fase canônica não pode aparecer duas vezes no cronograma.",
            g => g.FasesAdicionais.Add(Fase(2, Id(200), agrupaEtapas: false))),
        [Violacao.FaseAgrupadoraSemEtapa] = new(
            "ProcessoSeletivo.AvaliacaoSemEtapa",
            "Há uma fase que agrupa etapas no cronograma restaurado, mas nenhuma etapa acontece nela.",
            g => g.FaseDasEtapas = FaseForaDoCronograma),
        [Violacao.EtapaSemFaseAlguma] = new(
            "ProcessoSeletivo.EtapaSemFaseDeAvaliacao",
            "Há etapa pontuada no grafo restaurado sem fase declarada, e nenhuma fase agrupa etapas.",
            g => g.FaseDaBaseAgrupaEtapas = false),
        [Violacao.ArvoreDeExigenciasInconsistente] = new(
            "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido",
            "Uma folha da árvore de satisfação restaurada referencia um DocumentoExigido que não existe na configuração restaurada.",
            g => g.Nos.Add(FolhaDeDocumentoAusenteDoGrafo())),
    };

    private static void ComEtapaDoEnemEliminadaPorAusencia(GrafoEmMontagem grafo)
    {
        grafo.ClassificacaoPeloEnem = true;
        grafo.EtapasAdicionais.Add(EtapaNotaEnemCongelada(Id(60), 60));
        grafo.Eliminacoes.Add(EliminacaoPorAusenciaNaEtapa(Id(60)));
    }
}
