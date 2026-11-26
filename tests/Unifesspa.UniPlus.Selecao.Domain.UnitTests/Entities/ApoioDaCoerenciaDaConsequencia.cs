namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
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
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, naturezaLegal, composicaoVagas) with { RegraRemanejamento = RemanejamentoDaNatureza(naturezaLegal), AcaoQuandoIndeferido = acaoQuandoIndeferido }, naturezaLegal == NaturezaLegalModalidade.AcaoAfirmativa ? 10 : null).Value!;

    private static RegraRemanejamentoModalidade? RemanejamentoDaNatureza(NaturezaLegalModalidade naturezaLegal) =>
        naturezaLegal == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null;

    /// <summary>
    /// A matriz legal 8×7 completa, fallback AC — usada por
    /// <see cref="NovoProcessoComModalidade"/> sempre que a modalidade do chamador é
    /// <see cref="NaturezaLegalModalidade.CotaReservada"/>: a cascata de remanejamento de cotas (UNI-REQ-0056)
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

        ReferenciaRegra regraCascata = ReferenciaRegra.De(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "v1", HashFixo);
        return ConfiguracaoCascataRemanejamento.Criar(regraCascata, ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    internal static ProcessoSeletivo NovoProcessoComModalidade(ModalidadeSelecionada modalidade)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
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
            DefinirDistribuicao(processo, DistribuicaoInstitucional(modalidade));
            return;
        }

        DefinirDistribuicao(processo, DistribuicaoDaLei12711(modalidade));
        processo.DefinirCascataRemanejamento(CascataLegalCompleta()).IsSuccess.Should().BeTrue();
    }

    private static DistribuicaoDeVagasDeclarada DistribuicaoInstitucional(ModalidadeSelecionada modalidade) => new()
    {
        VagasOfertadasBase = (modalidade.QuantidadeDeclarada ?? 0) + 40,
        PercentualDaReservaLegal = 1m,
        RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo),
        Modalidades = [modalidade, ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(
            ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas), 40).Value!],
    };

    private static void DefinirDistribuicao(ProcessoSeletivo processo, DistribuicaoDeVagasDeclarada declarada)
    {
        Result<ConfiguracaoDistribuicaoVagas> resultado = ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), declarada, new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina });
        resultado.IsSuccess.Should().BeTrue("{0}", string.Join(", ", resultado.Errors));
        processo.DefinirDistribuicaoVagas([resultado.Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }

    /// <summary>A distribuição pela Lei 12.711, com a modalidade do chamador no lugar da federal padrão.</summary>
    private static DistribuicaoDeVagasDeclarada DistribuicaoDaLei12711(ModalidadeSelecionada modalidade) => new()
    {
        VagasOfertadasBase = 40,
        PercentualDaReservaLegal = 0.5m,
        RegraDeDistribuicao = ReferenciaRegra.De(RegraDeDistribuicaoDeVagas.Lei12711.Codigo(), "v1", HashFixo),
        RegraDeAjuste = ReferenciaRegra.De("AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "v1", HashFixo),
        ReferenciaDemografica = ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!,
        Modalidades = ModalidadesFederaisEAmpla(modalidade),
    };

    /// <summary>As 8 modalidades federais (com a do chamador no lugar da padrão) mais a ampla concorrência como restante.</summary>
    private static List<ModalidadeSelecionada> ModalidadesFederaisEAmpla(ModalidadeSelecionada modalidade) =>
    [
        .. ModalidadesFederaisLei12711.Codigos.Where(codigo => codigo != modalidade.Codigo).Select(CotaFederalPadrao),
        modalidade,
        ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "base legal" }).Value!,
    ];

    private static ModalidadeSelecionada CotaFederalPadrao(string codigo) => ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDaReservaLegal)).Value!;

    private static void DefinirOQueFaltaParaPublicar(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo),
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();

        // Publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        RegrasDeAplicabilidadeDeTeste.DerivarOQueAOpcaoPelasCotasCita(processo);
    }

    internal static DocumentoExigido ExigenciaCondicionalPorModalidadeComConsequencia(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string tipoDocumentoNome, string tipoDocumentoCategoria,
        string modalidadeCodigo, string consequenciaDoDescumprimento, string fatoDeModalidade = "MODALIDADE") =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), tipoDocumentoCodigo, tipoDocumentoNome, tipoDocumentoCategoria, ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(exigidoNaFaseId, null, FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao)))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, fatoDeModalidade, Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, consequenciaDoDescumprimento, [BaseLegalResolvidaQualquer()]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;
}
