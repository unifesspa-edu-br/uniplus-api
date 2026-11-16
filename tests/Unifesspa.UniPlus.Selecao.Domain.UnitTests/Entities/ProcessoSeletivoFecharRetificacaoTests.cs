namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static RetificacaoDeTeste;

/// <summary>
/// Fechamento da sessão editorial de retificação em <see cref="ProcessoSeletivo.FecharRetificacao"/>: a
/// versão seguinte só é congelada se a configuração editada passa pelos mesmos portões da publicação, e
/// a recusa deixa a sessão aberta.
/// </summary>
public sealed class ProcessoSeletivoFecharRetificacaoTests
{
    // ── Story #554/PR #903 (issue #548) — guarda B-01 removida: o bloco deixou de ser stub ──

    [Fact(DisplayName = "Story #554/PR #903: fechar retificação com exigência configurada durante a sessão é aceito — a guarda B-01 foi removida")]
    public void FecharRetificacao_ExigenciaConfiguradaNaSessao_Aceita()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Incluir exigência documental", versaoAbertura, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-conta-123", clock.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

        Guid faseId = processo.CronogramaFases.Single().Id;
        // Story #554, PR #898: a exigência determina resultado (Obrigatorio=true), então
        // precisa de base legal RESOLVIDO para satisfazer o 5º item de AvaliarConformidade.
        DocumentoExigidoBaseLegal baseLegal = DocumentoExigidoBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            faseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [baseLegal], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;
        processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

        clock.Avancar(TimeSpan.FromMinutes(1));
        Result<VersaoConfiguracao> resultado = processo.FecharRetificacao(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-conta-123",
            PrecondicaoIfMatch.Curinga, clock, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message ?? "o bloco documentosExigidos.exigencias deixou de ser stub — nada mais bloqueia este fechamento");
        processo.Rascunho.Should().BeNull("o fechamento aceito encerra a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação com uma fase sem unidade responsável recusa e a sessão segue aberta")]
    public void FecharRetificacao_FaseSemUnidadeResponsavel_RecusaESessaoSegueAberta()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoPublicado(clock, out VersaoConfiguracao versaoAbertura);
        clock.Avancar(TimeSpan.FromMinutes(1));

        processo.AbrirRetificacao(
            "Trocar a unidade responsável pela matrícula", versaoAbertura, identificadorDaVersaoBase: null,
            VersoesPublicadasDeTeste.SoAVigente(processo), "user-conta-123", clock.GetUtcNow())
            .IsSuccess.Should().BeTrue();

        // Trocar a unidade depois de publicar é retificação, e a sessão aceita o rascunho sem ela:
        // quem recusa é o fechamento, que congela a versão nova.
        FaseCronograma atual = processo.CronogramaFases.Single();
        FaseCronograma semUnidade = FaseCronograma.Criar(
            atual.Ordem, atual.FaseCanonicaOrigemId, atual.Codigo, atual.OrigemData,
            atual.AgrupaEtapas, atual.PermiteComplementacao, atual.ColetaInscricao, atual.ColetaSolicitacaoIsencao,
            atual.Inicio, atual.Fim, [.. atual.Produtos], atual.FaseConcluinteCodigo, atual.EmiteParecerIndividual,
            [.. atual.BancasRequeridas], atual.RegraRecurso, unidadeResponsavel: null).Value!;
        Result cronograma = processo.DefinirCronogramaFases([semUnidade], [], PrecondicaoIfMatch.Curinga);
        cronograma.IsSuccess.Should().BeTrue(cronograma.Error?.Message);

        clock.Avancar(TimeSpan.FromMinutes(1));
        Result<VersaoConfiguracao> resultado = processo.FecharRetificacao(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-conta-123",
            PrecondicaoIfMatch.Curinga, clock, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FaseSemUnidadeResponsavel");
        processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    // ── Story #554 (PR #903, issue #548) — CA-05 (5/5): reavaliação após mudança de gatilho ──

    private static ProcessoSeletivo NovoProcessoComDuasModalidades()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // AcaoQuandoIndeferido = RECLASSIFICA_AC: heteroidentificação/cota reprovada
        // reclassifica o candidato para ampla concorrência, nunca elimina (RN, concorrência
        // dupla — Lei 14.723/2023).
        ModalidadeSelecionada ppi = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "LB_PPI", descricao: null,
            naturezaLegal: NaturezaLegalModalidade.CotaReservada, composicaoVagas: ComposicaoVagasModalidade.DentroDoVr,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.SegueCascata,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), acaoQuandoIndeferido: "RECLASSIFICA_AC", baseLegal: "Lei 12.711/2012", quantidadeDeclarada: null).Value!;
        // As demais 7 modalidades federais (Story #575, cascata de remanejamento de cotas, UNI-REQ-0056) — SegueCascata só é
        // válida no regime federal completo, com cascata legal por trás.
        List<ModalidadeSelecionada> demaisFederais =
        [
            .. ModalidadesFederaisLei12711.Codigos
                .Where(codigo => codigo != "LB_PPI")
                .Select(codigo => ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), codigo, null, NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr,
                    null, RegraRemanejamentoModalidade.SegueCascata, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.CotaReservada), null, "Lei 12.711/2012", quantidadeDeclarada: null).Value!),
        ];
        ModalidadeSelecionada ac = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(), codigo: "AC", descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla, composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null, regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null, remanejamentoPar: null, remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), acaoQuandoIndeferido: null, baseLegal: "Res. Unifesspa 532/2021", quantidadeDeclarada: null).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 0.5m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Lei12711, "v1", HashFixo).Value!,
            regraAjuste: ReferenciaRegra.Criar("RECONCILIACAO-VAGAS-ART11-PU", "v1", HashFixo).Value!,
            referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022").Value!,
            modalidades: [.. demaisFederais, ppi, ac], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        List<DestinoRemanejamento> destinosCascata = [];
        foreach (string origem in ModalidadesFederaisLei12711.Codigos)
        {
            string[] ordemDestinos = [.. ModalidadesFederaisLei12711.Codigos.Where(o => o != origem)];
            for (int i = 0; i < ordemDestinos.Length; i++)
            {
                destinosCascata.Add(DestinoRemanejamento.Criar(origem, i + 1, ordemDestinos[i]).Value!);
            }
        }

        ReferenciaRegra regraCascata = ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", HashFixo).Value!;
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            regraCascata, ModalidadesFederaisLei12711.Ac, destinosCascata).Value!;
        processo.DefinirCascataRemanejamento(cascata, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo).Value!,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo).Value!,
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();


        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        RegrasDeAplicabilidadeDeTeste.DerivarOQueAOpcaoPelasCotasCita(processo);
        return processo;
    }

    private static DocumentoExigidoBaseLegal BaseLegalResolvidaQualquer() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    private static DocumentoExigido ExigenciaCondicionalPorModalidadeComConsequencia(Guid exigidoNaFaseId, string modalidadeCodigo, string consequenciaIndeferimento) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "AUTODECLARACAO_ETNICO_RACIAL",
            tipoDocumentoNome: "Autodeclaração étnico-racial",
            tipoDocumentoCategoria: "HETEROIDENTIFICACAO", ProtecaoDoDocumentoDeTeste.Pessoal,
            aplicabilidade: Aplicabilidade.Condicional,
            obrigatorio: false,
            consequenciaIndeferimento: consequenciaIndeferimento,
            condicoes: [CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!],
            basesLegais: [BaseLegalResolvidaQualquer()], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    [Fact(DisplayName = "CA-05 (5/5): mudar o gatilho de AC para LB_PPI numa retificação reavalia a coerência consequência↔ação — não fica cacheada da publicação anterior")]
    public void FecharRetificacao_MudancaDeGatilhoTornaConsequenciaIncoerente_Reavalia()
    {
        RelogioManual clock = Relogio();
        ProcessoSeletivo processo = NovoProcessoComDuasModalidades();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "AC", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            NovosDados(), BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-conta-123", clock, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(
            publicacao.Error?.Message ?? "AC não declara AcaoQuandoIndeferido — nada com que a exigência seja incoerente");
        VersaoConfiguracao versaoAbertura = publicacao.Value!;
        processo.DequeueDomainEvents();
        clock.Avancar(TimeSpan.FromMinutes(1));

        Result<RascunhoRetificacao> abertura = processo.AbrirRetificacao(
            "Estender exigência de heteroidentificação para a cota PPI", versaoAbertura, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(processo), "user-conta-123", clock.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

        // Mesma consequência ELIMINA — só o gatilho muda, de AC (ação null, coerente) para
        // LB_PPI (ação RECLASSIFICA_AC, incoerente com ELIMINA).
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidadeComConsequencia(faseId, "LB_PPI", "ELIMINA"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

        clock.Avancar(TimeSpan.FromMinutes(1));
        Result<VersaoConfiguracao> resultado = processo.FecharRetificacao(
            NovosDados(), versaoAbertura, BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo, "user-conta-123",
            PrecondicaoIfMatch.Curinga, clock, ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);

        resultado.IsFailure.Should().BeTrue(
            "a coerência é recomputada a cada fechamento, a partir da coleção real de exigências — não fica " +
            "cacheada do estado coerente aprovado na publicação anterior");
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga");
        processo.Rascunho.Should().NotBeNull("o fechamento recusado não encerra a sessão editorial");
    }
}
