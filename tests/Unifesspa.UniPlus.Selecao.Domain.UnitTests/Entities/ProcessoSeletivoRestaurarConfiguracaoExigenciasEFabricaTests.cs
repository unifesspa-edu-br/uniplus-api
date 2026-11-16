namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoRestaurarConfiguracao;

public sealed class ProcessoSeletivoRestaurarConfiguracaoExigenciasEFabricaTests
{
    [Fact(DisplayName = "issue #563 (CA-13, bloco default/D5): restaurar repõe ausência mesmo que a sessão editorial tenha gravado uma configuração explícita")]
    public void Restauracao_RepoeAusenciaDeConfiguracaoDivulgacaoQuandoOGrafoCongeladoENulo()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Editar a divulgação", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        processo.ConfiguracaoDivulgacao.Should().NotBeNull("pré-condição: a sessão editorial gravou uma configuração explícita");

        // O grafo congelado não traz configuração alguma (D5: o certame nunca configurou
        // divulgação, ou configurou exatamente o default) — a restauração não fabrica entidade.
        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, Grafo(configuracaoDivulgacao: null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.ConfiguracaoDivulgacao.Should().BeNull(
            "D5: a ausência no grafo congelado é o efetivo default — repor precisa devolver o processo a essa " +
            "ausência, não preservar a configuração explícita que a sessão descartada editou");
    }

    private static FaseCronograma FaseCongeladaDeResultadoFinal(Guid faseCongeladaId) => FaseCronograma.Reidratar(
        faseCongeladaId,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "Story #554, PR #903 (achado de revisão P2) — restaurar remapeia ExigidoNaFaseId/ReferenciaTemporalFatos.FaseId para a fase VIVA quando a sessão editorial trocou a fase da mesma Ordem")]
    public void Restauracao_RemapeiaReferenciasDeFaseParaAInstanciaViva()
    {
        ProcessoSeletivo processo = ProcessoPublicado(TiposDeProcessoDeTeste.SiSU);
        VersaoConfiguracao versao = VersaoDo(processo);

        processo.AbrirRetificacao("Trocar a fase da Ordem 1", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "testes", DateTimeOffset.UnixEpoch)
            .IsSuccess.Should().BeTrue();

        // A sessão editorial troca a fase da Ordem 1 por uma fase GENUINAMENTE diferente
        // (FaseCanonicaOrigemId novo, não reaproveita a identidade estável da fase
        // publicada) — DefinirCronogramaFases reconcilia por FaseCanonicaOrigemId (CA-04),
        // então isto insere uma instância NOVA em vez de atualizar a existente no lugar.
        FaseCronograma faseTrocada = FaseDeResultadoFinal();
        processo.DefinirCronogramaFases([faseTrocada], [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        Guid faseVivaId = processo.CronogramaFases.Single().Id;

        // O grafo CONGELADO referencia a fase que existia QUANDO a versão foi publicada —
        // um Id diferente do da fase viva acima (Reidratar preserva o Id congelado no
        // envelope 1.2, ADR-0110 D2), mas na MESMA Ordem — o caso que a reconciliação por
        // Ordem de AplicarGrafo reusa a instância viva em vez da decodificada.
        Guid faseCongeladaId = Guid.CreateVersion7();
        FaseCronograma faseCongelada = FaseCongeladaDeResultadoFinal(faseCongeladaId);

        DocumentoExigido documentoCongelado = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), exigidoNaFaseId: faseCongeladaId, tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE", tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal, aplicabilidade: Aplicabilidade.Geral, obrigatorio: true,
            consequenciaIndeferimento: null, condicoes: [], basesLegais: [],
            idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null);

        ReferenciaTemporalFatos referenciaCongelada = ReferenciaTemporalFatos.Criar(
            ReferenciaTipo.FimFase, null, faseCongeladaId).Value!;

        GrafoConfiguracao grafoCongelado = new(
            etapas: [EtapaCongeladaDeProvaObjetiva(EtapaCongelada, "Prova", ordem: 1)],
            atendimentoEspecializado: AtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [Distribuicao()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: Classificacao([]),
            cronogramaFases: [faseCongelada],
            documentosExigidos: [documentoCongelado],
            nosExigencia: [],
            referenciaTemporalFatos: referenciaCongelada,
            numeroDeOpcoesDeCurso: 1);

        Result resultado = processo.RestaurarConfiguracaoCongelada(versao, grafoCongelado);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

        FaseCronograma faseReposta = processo.CronogramaFases.Single();
        faseReposta.Id.Should().Be(faseVivaId,
            "a reconciliação de fases é por Ordem, não por Id (ux_fases_cronograma_processo_ordem) — a instância " +
            "VIVA sobrevive, não a decodificada");

        processo.DocumentosExigidos.Single().ExigidoNaFaseId.Should().Be(faseVivaId,
            "sem o remapeamento, o documento restaurado ficaria com ExigidoNaFaseId apontando para o Id " +
            "CONGELADO — ausente de CronogramaFases após a restauração (achado de revisão da PR #903)");

        processo.ReferenciaTemporalFatos!.FaseId.Should().Be(faseVivaId,
            "mesmo raciocínio do documento exigido: FIM_FASE precisa apontar para uma fase que realmente existe " +
            "em CronogramaFases após a restauração");
    }

    // ── Fábrica de cenários ──
}
