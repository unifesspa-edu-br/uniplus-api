namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os processos de inscrição que os testes publicam: o conjunto básico, os campos próprios e a modalidade que deriva de resposta.</summary>
internal static class ProcessosDeInscricaoDeTeste
{
    private static FaseCronograma FaseDeIsencaoDaTaxa() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "ISENCAO_DA_TAXA", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: true),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    internal static ProcessoSeletivo NovoProcessoDeInscricao(
        string nome,
        AtendimentoEspecializado? atendimento = null,
        IReadOnlyList<FatoColetado>? camposAdicionais = null,
        IReadOnlyList<ModalidadeSelecionada>? modalidades = null,
        string? fatoDoFator = null,
        bool comIsencao = false)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirAtendimentoEspecializado(atendimento ?? AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)), Modalidades = modalidades ?? [modalidade] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!],
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)), null, null,
            ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)), [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseDaInscricao = FaseDeResultadoFinal();
        List<FaseCronograma> fases = [faseDaInscricao];
        if (comIsencao)
        {
            // A janela de isenção abre com a inscrição e fecha antes dela: é fase à parte.
            fases.Add(FaseDeIsencaoDaTaxa());
        }

        processo.DefinirCronogramaFases(fases, [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirTaxaInscricao(
            (comIsencao
                ? ConfiguracaoTaxaInscricao.Criar(cobra: true, valor: 100m, fundamentosCodigos: [FundamentoIsencaoCodigo.CarenciaSocioeconomica])
                : ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null)).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
        if (fatoDoFator is not null)
        {
            ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(ReferenciaRegra.De(RegraBonusCodigo.Multiplicativo, "v1", new string('d', 64)), 1.20m, null, fatoDoFator, new BaseLegalCongelada(Guid.CreateVersion7(), "PORTARIA_UNIFESSPA_NO_2514_2023", "PORTARIA", "Portaria de teste", "Inclusão regional", [("1504208", "Marabá", "PA")])).Value!;
            processo.DefinirBonusRegional(aplica: true, bonus, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        GrupoColetado composicaoFamiliar = GrupoColetado.Criar(
            "COMPOSICAO_FAMILIAR", 0, etapaCodigo: FormularioDeTeste.Secao, "Composição familiar", minimo: 0, maximo: null, exibicao: null,
            Obrigatoriedade.Sempre,
            [
                FatoColetado.Criar(
                    "RG_NUMERO_MEMBRO", 0, "RG do membro", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
                FatoColetado.Criar(
                    "PARENTESCO", 1, "Parentesco", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
                    classificacaoProtecao: "PESSOAL", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            ],
            FinalidadeFormulario.Inscricao).Value!;

        FatoColetado rg = FatoColetado.Criar(
            "RG_NUMERO", 0, "RG", TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "IDENTIFICADOR", formato: "LIVRE", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        processo.DefinirItens(
            [rg, .. camposAdicionais ?? []],
            PrecondicaoIfMatch.Ausente,
            grupos: [composicaoFamiliar]).IsSuccess.Should().BeTrue();

        if (comIsencao)
        {
            processo.DefinirFormulario(
                    FinalidadeFormulario.IsencaoTaxa, processo.CronogramaFases.Single(f => f.ColetaSolicitacaoIsencao).Id, null,
                    FormularioDeTeste.Etapas(FinalidadeFormulario.IsencaoTaxa), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
            processo.DefinirFatosColetados(
                    FinalidadeFormulario.IsencaoTaxa,
                    [FatoColetado.Criar(
                        "BOLSISTA", 0, "Bolsista", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
                        etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!],
                    PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }

        processo.DefinirTitulo("Formulário de Inscrição").IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelope.Termo("DECLARACAO_VERACIDADE", 0)]).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Um processo publicado em que a modalidade deriva de uma resposta: a ampla concorrência só a quem
    /// declara a deficiência, para as três situações caberem numa oferta só.
    /// </summary>
    internal static ProcessoSeletivo ProcessoQueDerivaAModalidade(string nome)
    {
        FatoColetado concorrerPcd = FatoColetado.Criar(
            "CONCORRER_PCD", 1, "Você quer concorrer às vagas para pessoa com deficiência?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "SENSIVEL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, Clausulas([("CONCORRER_PCD", true)]), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return NovoProcessoDeInscricao(nome, camposAdicionais: [concorrerPcd], modalidades: [ampla]);
    }

    internal static ProcessoSeletivo ProcessoComFator(string nome, bool comIsencao = false)
    {
        FatoColetado pedeOFator = FatoColetado.Criar(
            "PEDE_O_FATOR", 1, "Você solicita o Fator de Atenuação?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        return NovoProcessoDeInscricao(nome, camposAdicionais: [pedeOFator], fatoDoFator: "PEDE_O_FATOR", comIsencao: comIsencao);
    }

    internal static PredicadoDnf Clausulas(params (string Fato, bool Valor)[][] clausulas) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. clausulas.SelectMany(static (atomos, indice) => atomos.Select(a =>
            (indice, CondicaoDnf.Criar(a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!)))]).Value!;

    internal static ProcessoSeletivo ProcessoComUmCampo(string nome, string fato, Obrigatoriedade obrigatoriedade) =>
        NovoProcessoDeInscricao(nome, camposAdicionais:
        [
            FatoColetado.Criar(
                fato, 1, fato, TipoRenderizacao.Texto, obrigatoriedade, null,
                classificacaoProtecao: "PESSOAL", etapaCodigo: FormularioDeTeste.Secao, formato: "LIVRE", finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ]);

    /// <summary>O processo que deriva a modalidade da resposta a CONCORRER_PCD e ainda pede <paramref name="fato"/>, obrigatório.</summary>
    internal static ProcessoSeletivo ProcessoQueDerivaAModalidadeComUmCampo(string nome, string fato)
    {
        FatoColetado concorrerPcd = FatoColetado.Criar(
            "CONCORRER_PCD", 1, "Você quer concorrer às vagas para pessoa com deficiência?", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "SENSIVEL", etapaCodigo: FormularioDeTeste.Secao, finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        FatoColetado pedido = FatoColetado.Criar(
            fato, 2, fato, TipoRenderizacao.Texto, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL", etapaCodigo: FormularioDeTeste.Secao, formato: "LIVRE", finalidade: FinalidadeFormulario.Inscricao, hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        ModalidadeSelecionada ampla = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, Clausulas([("CONCORRER_PCD", true)]), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        return NovoProcessoDeInscricao(nome, camposAdicionais: [concorrerPcd, pedido], modalidades: [ampla]);
    }
}
