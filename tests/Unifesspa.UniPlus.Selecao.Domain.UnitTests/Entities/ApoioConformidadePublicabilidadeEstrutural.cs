namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioConformidadePublicabilidadeEstrutural
{
    /// <summary>A matriz legal completa (8×7), fallback AC — cada origem resolve nas outras sete, todas ofertadas.</summary>
    internal static ConfiguracaoCascataRemanejamento CascataCompleta()
    {
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in OrigensFederais)
        {
            string[] ordemDestinos = [.. OrigensFederais.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinos.Add(Destino(origem, i + 1, ordemDestinos[i]));
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(RegraCascataRemanejamento(), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>Só os itens de <see cref="ProcessoSeletivo.AvaliarConformidade"/> que se espera ver vermelhos.</summary>
    internal static void SoEstesItensVermelhos(ProcessoSeletivo processo, params string[] itensVermelhos) =>
        SoEstesItensVermelhos(processo, ContextoDeContagemDePrazos.SemCalendario, itensVermelhos);

    internal static void SoEstesItensVermelhos(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto, params string[] itensVermelhos) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao).Where(static i => !i.Ok).Select(static i => i.Codigo)
            .Should().BeEquivalentTo(itensVermelhos);

    internal static CondicaoGatilho GatilhoFaixaEtaria() =>
        CondicaoGatilho.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!;

    internal static readonly (string? Codigo, string? Rotulo) GrupoHumanisticaI = ("HUMANISTICA_I", "Humanística I");

    internal static ConfiguracaoClassificacao ClassificacaoEnemLocal(IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'), Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'b'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao, quadroPesoAreaEnem: quadro, escalaNotaFinal: 100m).Value!;

    internal static ProcessoSeletivo ProcessoEnemLocal(
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> ofertas,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro = null)
    {
        ProcessoSeletivo processo = ProcessoConformeComEtapaQueCompoeNota();
        processo.DefinirDistribuicaoVagas(ofertas, FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoEnemLocal(quadro ?? QuadroPesoAreaEnemDeTeste.Completo()), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        // Classificação baseada em ENEM exige ao menos uma edição admitida (issue #1620) — sem
        // ela, o item "classificacao_edicao_enem_nao_declarada" ficaria vermelho e interferiria
        // nos testes desta classe, que focam nos itens do quadro de pesos por área.
        processo.DefinirOpcoesDeclaradas("EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>A fábrica recusa quadro vazio; o EF hidrata a linha sem passar por ela.</summary>
    internal static void EsvaziarQuadro(ConfiguracaoClassificacao classificacao) =>
        ((List<GrupoPesoAreaEnemCongelado>)typeof(ConfiguracaoClassificacao)
            .GetField("_quadroPesoAreaEnem", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(classificacao)!).Clear();

    internal static ItemConformidade Item(ProcessoSeletivo processo, string codigo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao).Single(i => i.Codigo == codigo);

    /// <summary>
    /// Processo conforme com uma etapa que compõe nota e a fase que a agrupa: sem elas o
    /// divisor da média é zero e a publicação recusa antes, por outro item. A oferta tem um
    /// grupo do quadro de teste, que a classificação ENEM com cálculo local exige.
    /// </summary>
    internal static ProcessoSeletivo ProcessoConformeComEtapaQueCompoeNota()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaAmpla(10, GrupoTecnologica)]);
        processo.DefinirCronogramaFases(
            [FaseCronograma.Criar(
                new OrigemCanonicaDaFase(
                    Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
                new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
                new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
                bancasRequeridas: []).Value!],
            [], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(
            [EtapaDeProvaObjetiva("Prova", CaraterEtapa.Classificatoria, 1, 1m, null, "RESULTADO_FINAL")], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    internal static EtapaProcesso EtapaDeProvaObjetiva(
        string nome, CaraterEtapa carater, int? ordem, decimal? peso, decimal? notaMinima, string? faseCodigo) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: carater, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: notaMinima, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
    internal static FatoColetado FatoDeOpcaoDeTeste() => FatoColetado.Criar(
        "FATO_OPCAO_DE_TESTE", 0, "Fato de opção do processo", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null,
        origemValores: OrigemValoresColeta.OpcoesDoProcesso, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static OpcaoDeclaradaFato Opcao(string codigo, int ordem = 0) =>
        OpcaoDeclaradaFato.Criar("FATO_OPCAO_DE_TESTE", codigo, $"Opção {codigo}", ordem).Value!;

    internal static AtendimentoEspecializado OfertaComCondicaoPcd() => AtendimentoEspecializado.Criar(
        [CondicaoOfertada.Criar(Guid.CreateVersion7(), AtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
        [],
        []).Value!;

    /// <summary>
    /// As oito cotas da lei, a ampla e a ação afirmativa de PcD retirando duas vagas da ampla, cada uma
    /// com a regra de aplicabilidade dada.
    /// </summary>
    internal static ProcessoSeletivo ProcessoComCotaEAcaoAfirmativa(PredicadoDnf regraDasCotas, PredicadoDnf regraDaAcaoAfirmativa)
    {
        ModalidadeSelecionada acPcd = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC_PCD", null, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade,
            ModalidadesFederaisLei12711.Ac, RegraRemanejamentoModalidade.DestinoUnico, ModalidadesFederaisLei12711.Ac, null, null, regraDaAcaoAfirmativa, null, "base legal", 2).Value!;
        ConfiguracaoDistribuicaoVagas oferta = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada
            {
                VagasOfertadasBase = 40,
                PercentualDaReservaLegal = 0.5m,
                RegraDeDistribuicao = RegraLei12711(),
                RegraDeAjuste = RegraAjusteArt11(),
                ReferenciaDemografica = Demografica(),
                Modalidades = [
                .. OrigensFederais.Select(codigo =>
                    Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null, regra: regraDasCotas)),
                Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
                acPcd,
            ]
            },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

        ProcessoSeletivo processo = ProcessoConforme([oferta]);
        processo.DefinirCascataRemanejamento(CascataCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    internal static bool ItemDaExclusividadeOk(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao)
            .Single(i => i.Codigo == "derivacao_cota_e_acao_afirmativa_juntas").Ok;

    internal static ProcessoSeletivo CotaEAcaoAfirmativaPor(string fato, object valorDaCota, object valorDaAcaoAfirmativa) =>
        ProcessoComCotaEAcaoAfirmativa(Exige((fato, valorDaCota)), Exige((fato, valorDaAcaoAfirmativa)));

    internal static PredicadoDnf Exige(params (string Fato, object Valor)[] atomos) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. atomos.Select(static a => (0, CondicaoDnf.Criar(
            a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))]).Value!;

    internal static readonly string HashFixo = new('a', 64);

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    internal static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) =>
        Publicar(processo, ContextoDeContagemDePrazos.SemCalendario);

    internal static Result<VersaoConfiguracao> Publicar(
        ProcessoSeletivo processo, ContextoDeContagemDePrazos contexto) => processo.Publicar(
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System, contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

    internal static ConfiguracaoClassificacao ClassificacaoImportada() => ConfiguracaoClassificacao.Criar(
        Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
        Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;

    internal static ModalidadeSelecionada Modalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        RegraRemanejamentoModalidade remanejamento,
        int? quantidadeDeclarada,
        string? acaoQuandoIndeferido = null,
        PredicadoDnf? regra = null) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null, natureza,
            natureza == NaturezaLegalModalidade.Ampla ? ComposicaoVagasModalidade.RestanteDasVagasOfertadas : ComposicaoVagasModalidade.DentroDaReservaLegal,
            null, remanejamento, null, null, null, regra ?? RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza), acaoQuandoIndeferido, "base legal", quantidadeDeclarada).Value!;

    internal static readonly (string? Codigo, string? Rotulo) GrupoTecnologica = ("TECNOLOGICA", "Tecnológica");

    internal static ConfiguracaoDistribuicaoVagas DistribuicaoAmpla(int vagasOfertadasBase, string? acaoQuandoIndeferido = null) =>
        OfertaAmpla(vagasOfertadasBase, grupoAreaEnem: null, acaoQuandoIndeferido);

    internal static ConfiguracaoDistribuicaoVagas OfertaAmpla(
        int vagasOfertadasBase, (string? Codigo, string? Rotulo)? grupoAreaEnem, string? acaoQuandoIndeferido = null) =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = vagasOfertadasBase, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [Modalidade("AC", NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, vagasOfertadasBase, acaoQuandoIndeferido)] },
            new FatosDaOfertaNoCadastro { GrupoAreaEnem = grupoAreaEnem, OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    /// <summary>
    /// Os itens vermelhos do checklist, sem os do formulário: os testes que isolam uma razão de
    /// recusa montam processos sem formulário, ou com o formulário mínimo sem a fase de inscrição, e
    /// os itens do formulário ficariam vermelhos por isso — legitimamente, mas fora do que o teste
    /// isola.
    /// </summary>
    internal static IEnumerable<ItemConformidade> VermelhosForaDoFormulario(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.AvaliarConformidade(EstadoDaConfiguracao.De(processo), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum, CorretoresDeTeste.SemConsolidacao)
            .Where(static i => !i.Ok && !i.Codigo.StartsWith("formulario_", StringComparison.Ordinal));

    /// <summary>Fase mínima e coerente: não agrupa etapas (dispensável sem prova), produz resultado, não coleta inscrição.</summary>
    internal static FaseCronograma FaseBase(
        bool coletaInscricao = false, bool coletaSolicitacaoIsencao = false, Guid? faseCanonicaOrigemId = null,
        bool semUnidadeResponsavel = false) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, coletaInscricao, coletaSolicitacaoIsencao),
        new DecisoesDeCronogramaDaFase(1, Inicio: null, Fim: null, FaseConcluinteCodigo: null, UnidadeResponsavel: semUnidadeResponsavel ? null : UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// Processo estruturalmente conforme aos QUATRO gates: sem etapa (ImportacaoExterna +
    /// ClassificacaoImportada dispensam prova), atendimento, distribuição, classificação e
    /// cronograma coerente com uma única fase que produz resultado. Sem cascata (nenhuma
    /// modalidade SegueCascata) e sem exigências/regras de derivação/fatos coletados — os quatro
    /// gates ficam vazios/Ok até que o teste acrescente a única razão que quer isolar.
    /// </summary>
    internal static ProcessoSeletivo ProcessoConforme(IReadOnlyList<ConfiguracaoDistribuicaoVagas>? distribuicao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Matriz Estrutural", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS",
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas(distribuicao ?? [DistribuicaoAmpla(10)], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ClassificacaoImportada(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseBase()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01) — declarada
        // aqui para manter os QUATRO gates estruturais vazios/Ok até o teste acrescentar a
        // única razão que quer isolar.
        DeclararTaxaSemCobranca(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    internal static DocumentoExigidoBaseLegal BaseLegalResolvida() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    internal static NoExigenciaBaseLegal BaseLegalDoGrupo(StatusBaseLegal status) =>
        NoExigenciaBaseLegal.Criar("Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    internal static void ExigirPublicacaoRecusada(ProcessoSeletivo processo, string codigoDaRecusa)
    {
        Result<VersaoConfiguracao> resultado = Publicar(processo);
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoDaRecusa);
    }

    internal static void DeclararTaxaSemCobranca(ProcessoSeletivo processo)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    internal static ReferenciaRegra RegraLei12711() => Regra(RegraDistribuicaoVagasCodigo.Lei12711, 'f');

    internal static ReferenciaRegra RegraCascataRemanejamento() => Regra(RegraRemanejamentoCodigo.Cascata, 'e');

    internal static DestinoRemanejamento Destino(string origem, int ordem, string destino) =>
        DestinoRemanejamento.Criar(origem, ordem, destino).Value!;

    internal static ReferenciaReservaDemograficaSnapshot Demografica() =>
        ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!;

    internal static readonly string[] OrigensFederais =
    [
        ModalidadesFederaisLei12711.LbPpi, ModalidadesFederaisLei12711.LbQ, ModalidadesFederaisLei12711.LbPcd, ModalidadesFederaisLei12711.LbEp,
        ModalidadesFederaisLei12711.LiPpi, ModalidadesFederaisLei12711.LiQ, ModalidadesFederaisLei12711.LiPcd, ModalidadesFederaisLei12711.LiEp,
    ];

    /// <summary>
    /// As 8 modalidades federais (SegueCascata) + AC — INV-6 exige o conjunto completo sob Lei
    /// 12.711. Sem <c>quantidadeDeclarada</c>: sob o ramo federal, DENTRO_DA_RESERVA_LEGAL e RESTANTE_DAS_VAGAS_OFERTADAS
    /// são CALCULADAS pela fórmula do art. 10 — declarar quantidade aqui é recusado
    /// (QuantidadeCalculadaNaoInformavel).
    /// </summary>
    internal static List<ModalidadeSelecionada> AsOitoFederaisMaisAc() =>
    [
        .. OrigensFederais.Select(codigo =>
            Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueCascata, quantidadeDeclarada: null)),
        Modalidade(ModalidadesFederaisLei12711.Ac, NaturezaLegalModalidade.Ampla, RegraRemanejamentoModalidade.Nenhuma, quantidadeDeclarada: null),
    ];

    internal static ReferenciaRegra RegraAjusteArt11() => Regra("RECONCILIACAO-VAGAS-ARTIGO-11-PARAGRAFO-UNICO", 'd');

    internal static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta() => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 0.5m, RegraDeDistribuicao = RegraLei12711(), RegraDeAjuste = RegraAjusteArt11(), ReferenciaDemografica = Demografica(), Modalidades = AsOitoFederaisMaisAc() },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
}
