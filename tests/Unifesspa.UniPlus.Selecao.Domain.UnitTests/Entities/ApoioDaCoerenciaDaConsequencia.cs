namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

internal static class ApoioDaCoerenciaDaConsequencia
{
    /// <summary>
    /// Sob o regime federal (Lei 12.711), a quantidade de uma modalidade DentroDaReservaLegal ou
    /// RestanteDasVagasOfertadas é calculada pela fórmula — informá-la é recusado
    /// (ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel). Só a ação afirmativa,
    /// cuja quantidade o edital fixa fora da fórmula, precisa do 10.
    /// </summary>
    internal static ModalidadeSelecionada NovaModalidadeComAcao(
        string codigo, NaturezaLegalModalidade naturezaLegal, ComposicaoVagasModalidade composicaoVagas, string? acaoQuandoIndeferido) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: codigo, descricao: null, naturezaLegal: naturezaLegal,
            composicaoVagas: composicaoVagas, composicaoOrigemCodigo: null, regraRemanejamento: RemanejamentoDaNatureza(naturezaLegal),
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(naturezaLegal), acaoQuandoIndeferido: acaoQuandoIndeferido,
            baseLegal: "Lei 12.711/2012", quantidadeDeclarada: naturezaLegal == NaturezaLegalModalidade.AcaoAfirmativa ? 10 : null).Value!;

    private static RegraRemanejamentoModalidade RemanejamentoDaNatureza(NaturezaLegalModalidade naturezaLegal) =>
        naturezaLegal == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueCascata : RegraRemanejamentoModalidade.Nenhuma;

    /// <summary>
    /// A matriz legal 8×7 completa, fallback AC (Story #575) — usada por
    /// <see cref="NovoProcessoComModalidade"/> sempre que a modalidade do chamador é
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>: desde a #575, a cascata de remanejamento de cotas (UNI-REQ-0056)
    /// recusa <c>SegueCascata</c> fora do regime federal, então uma cota reservada só
    /// publica com uma cascata legítima por trás.
    /// </summary>
    internal static ConfiguracaoCascataRemanejamento CascataLegalCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in ModalidadesFederaisLei12711.Codigos)
        {
            string[] ordemDestinos = [.. ModalidadesFederaisLei12711.Codigos.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, ordemDestinos[i]).Value!);
            }
        }

        ReferenciaRegra regraCascata = ReferenciaRegra.De(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo);
        return ConfiguracaoCascataRemanejamento.Criar(regraCascata, ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    internal static ProcessoSeletivo NovoProcessoComModalidade(ModalidadeSelecionada modalidade)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        processo.DefinirCronogramaFases([FaseConforme()], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            ApoioDaPublicacaoDoProcessoSeletivo.EtapaDaProvaObjetiva("Prova Objetiva", CaraterEtapa.Classificatoria, 1m, faseCodigo: "RESULTADO_FINAL"),
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        DefinirDistribuicaoDaModalidade(processo, modalidade);
        DefinirOQueFaltaParaPublicar(processo);
        return processo;
    }

    private static void DefinirDistribuicaoDaModalidade(ProcessoSeletivo processo, ModalidadeSelecionada modalidade)
    {
        if (modalidade.NaturezaLegal != NaturezaLegalModalidade.CotaReservada)
        {
            DefinirDistribuicao(processo, new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = modalidade.QuantidadeDeclarada ?? 40,
                PercentualDaReservaLegal = 1m,
                RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo),
                Modalidades = [modalidade],
            });
            return;
        }

        DefinirDistribuicao(processo, DistribuicaoDaLei12711(modalidade));
        processo.DefinirCascataRemanejamento(CascataLegalCompleta()).IsSuccess.Should().BeTrue();
    }

    private static void DefinirDistribuicao(ProcessoSeletivo processo, DistribuicaoDeVagasDeclarada declarada)
    {
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), declarada, new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    /// <summary>A distribuição pela Lei 12.711, com a modalidade do chamador no lugar da federal padrão.</summary>
    private static DistribuicaoDeVagasDeclarada DistribuicaoDaLei12711(ModalidadeSelecionada modalidade) => new()
    {
        VagasOfertadasBase = 40,
        PercentualDaReservaLegal = 0.5m,
        RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Lei12711, "v1", HashFixo),
        RegraDeAjuste = ReferenciaRegra.De("RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO", "v1", HashFixo),
        ReferenciaDemografica = ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!,
        Modalidades = ModalidadesFederaisEAmpla(modalidade),
    };

    /// <summary>As 8 modalidades federais (com a do chamador no lugar da padrão) mais a ampla concorrência como restante.</summary>
    private static List<ModalidadeSelecionada> ModalidadesFederaisEAmpla(ModalidadeSelecionada modalidade) =>
    [
        .. ModalidadesFederaisLei12711.Codigos.Where(codigo => codigo != modalidade.Codigo).Select(CotaFederalPadrao),
        modalidade,
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), ModalidadesFederaisLei12711.Ac, null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "base legal", quantidadeDeclarada: null).Value!,
    ];

    private static ModalidadeSelecionada CotaFederalPadrao(string codigo) => ModalidadeSelecionada.Criar(
        Guid.CreateVersion7(), codigo, null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal,
        null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), null, "Lei 12.711/2012", quantidadeDeclarada: null).Value!;

    private static void DefinirOQueFaltaParaPublicar(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo),
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        // Publicar sem declarar cobrança de taxa é recusado (issue #1112, CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        RegrasDeAplicabilidadeDeTeste.DerivarOQueAOpcaoPelasCotasCita(processo);
    }

    internal static DocumentoExigido ExigenciaCondicionalPorModalidadeComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string modalidadeCodigo, string consequenciaIndeferimento, string fatoDeModalidade = "MODALIDADE") =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: tipoDocumentoCodigo,
            tipoDocumentoNome: tipoDocumentoNome,
            tipoDocumentoCategoria: tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [CondicaoGatilho.Criar(0, fatoDeModalidade, Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!],
            basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
}
