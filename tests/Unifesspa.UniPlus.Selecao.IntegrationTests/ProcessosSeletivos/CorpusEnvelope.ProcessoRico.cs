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
    /// O agregado mais rico que o modelo permite: três etapas (uma de cada caráter),
    /// atendimento povoado, bônus com teto, <b>as quatro</b> variantes de desempate,
    /// classificação local com regras de eliminação de <b>todas</b> as variantes — duas delas do
    /// <b>mesmo</b> código — e duas ofertas de curso, uma sob a Lei 12.711 (com
    /// referência demográfica e as nove modalidades federais) e outra institucional.
    /// </summary>
    /// <remarks>
    /// A multiplicidade é deliberada: um decoder que indexasse as regras <b>por código</b>
    /// — em vez de acumulá-las numa lista — perderia a segunda <c>ELIM-NOTA-MINIMA-ETAPA</c>
    /// e o segundo <c>DESEMPATE-MAIOR-NOTA-ETAPA</c> em silêncio. Um corpus com “uma de
    /// cada variante” não pegaria isso.
    /// </remarks>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA das coleções não ordenadas — nunca o conteúdo.</param>
    /// <param name="comArvoreSatisfacao">
    /// Opt-in: acrescenta a árvore de satisfação de documentos exigidos (<see cref="ArvoreSatisfacaoRica"/>).
    /// Fica fora por padrão porque as golden fixtures (<c>envelope-rico.json</c>) e os testes de
    /// round-trip congelam a forma de HOJE do corpus rico — sem árvore, <c>documentosExigidos.exigencias</c>
    /// e <c>arvoreSatisfacao</c> vazios. Populá-la incondicionalmente mudaria os bytes desse envelope de
    /// referência para todo consumidor de <see cref="ProcessoRico"/>, não só quem testa a permutação.
    /// </param>
    internal static ProcessoSeletivo ProcessoRico(int variante = 0, bool permutar = false, bool comArvoreSatisfacao = false)
    {
        ProcessoSeletivo processo = NovoProcessoRico(variante);
        DefinirCronogramaEEtapas(processo, variante, permutar);
        DefinirOfertaEClassificacao(processo, variante, permutar);
        DefinirFormularioDeInscricao(processo, permutar);
        DefinirCascataEDocumentos(processo, variante, permutar, comArvoreSatisfacao);
        DefinirTaxaEOpcoesDeCurso(processo, permutar);
        return processo;
    }

    // SiSU é baseado em ENEM — é o que admite ELIM-CORTE-REDACAO e ELIM-ZERO-EM-AREA.
    // O identificador é determinístico, como os ids das etapas: ele entra no envelope e a golden
    // congela os bytes. A variante o mantém único entre os testes que persistem o corpus.
    private static ProcessoSeletivo NovoProcessoRico(int variante)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Rico 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, UnidadeAdministradora,
            UnidadeAdministradoraDoCorpus(),
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            identificadorLegivel: IdentificadorLegivel.Criar($"ps-rico-2026-v{variante}").Value);
        DefinirAlgoritmoDeContagem(processo);
        return processo;
    }

    private static UnidadeAdministradoraSnapshot UnidadeAdministradoraDoCorpus() =>
        UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
            cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA").Value!;

    // O corpus tem fase com regra de recurso, e o prazo de interposição corre sobre dia
    // útil — logo a convenção de contagem é exigida para publicar.
    private static void DefinirAlgoritmoDeContagem(ProcessoSeletivo processo) =>
        processo.DefinirAlgoritmoContagemPrazo(
            AlgoritmoDeContagemDoCorpus(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static void DefinirCronogramaEEtapas(ProcessoSeletivo processo, int variante, bool permutar)
    {
        processo.DefinirCronogramaFases(
            Ordem([FaseInscricao(variante), FaseResultadoPreliminarComRecurso(variante, permutar), FaseResultadoFinal(variante)], permutar), [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(Ordem([
            EtapaObjetiva(variante, permutar),
            EtapaRedacao(variante),
            EtapaEntrevista(variante, permutar),
            EtapaDefesa(variante, permutar),
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static void DefinirOfertaEClassificacao(ProcessoSeletivo processo, int variante, bool permutar)
    {
        DefinirOfertaDeAtendimento(processo, permutar);
        processo.DefinirDistribuicaoVagas(
            Ordem([DistribuicaoLei12711(), DistribuicaoLei12711(OfertaDireito, ("HUMANISTICA_I", "Humanística I"))], permutar),
            FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DefinirBonusRegional(processo);
        DefinirCriteriosDeDesempate(processo, variante, permutar);
        DefinirClassificacao(processo, variante, permutar);
        // Classificação baseada em ENEM exige ao menos uma edição admitida (issue #1620).
        processo.DefinirOpcoesDeclaradas(
            "EDICAO_ENEM", [OpcaoDeclaradaFato.Criar("EDICAO_ENEM", "2026", "2026", 0).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private static void DefinirOfertaDeAtendimento(ProcessoSeletivo processo, bool permutar) =>
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar(
            condicoes: Ordem<OfertaCondicao>([
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000001"), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000002"), "LACTANTE", "Lactante"),
            ], permutar),
            recursos: Ordem<OfertaRecurso>([
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000001"), "LEDOR", "Ledor"),
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000002"), "PROVA_AMPLIADA", "Prova ampliada"),
            ], permutar),
            tiposDeficiencia: Ordem<OfertaTipoDeficiencia>([
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000001"), "DEFICIENCIA_VISUAL", "Deficiência visual"),
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000002"), "DEFICIENCIA_AUDITIVA", "Deficiência auditiva"),
            ], permutar)).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static void DefinirBonusRegional(ProcessoSeletivo processo) =>
        processo.DefinirBonusRegional(aplica: true, bonus: ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, 'b'),
            fator: 1.2000m,
            teto: 95.5000m,
            fatoElegibilidadeCodigo: FormularioDeTeste.FatoBooleanoDaInscricao,
            baseLegalBonusRegionalId: new Guid("baba0000-0000-4000-8000-000000000001"),
            tipoInstrumento: "PORTARIA",
            identificacao: "Portaria Unifesspa nº 2514/2023",
            descricao: "Institui inclusão regional para candidatos de municípios do interior",
            municipios:
            [
                ("1504208", "Marabá", "PA"),
                ("1501402", "Belém", "PA"),
            ]).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    // As CINCO variantes de args — e DUAS do mesmo código (MAIOR-NOTA-ETAPA em
    // ordens distintas), que um decoder indexado por código colapsaria em uma. A ordem
    // das áreas do desempate por área do ENEM não é a alfabética, para que uma
    // canonicalização que as reordenasse mudasse o envelope.
    private static void DefinirCriteriosDeDesempate(ProcessoSeletivo processo, int variante, bool permutar) =>
        processo.DefinirCriteriosDesempate(Ordem([
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.Idoso, 'c'), new ArgsDesempateIdoso(60)).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(EtapaId(1, variante))).Value!,
            CriterioDesempate.Criar(3, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(EtapaId(2, variante))).Value!,
            CriterioDesempate.Criar(4, Regra(CriterioDesempateCodigo.PredicadoFato, 'e'), new ArgsDesempatePredicadoFato(
                CondicaoDnf.Criar("escola_publica", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)).Value!,
            CriterioDesempate.Criar(5, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!,
            CriterioDesempate.Criar(6, Regra(CriterioDesempateCodigo.MaiorNotaAreaEnem, '8'), new ArgsDesempateMaiorNotaAreaEnem(["REDACAO", "MATEMATICA", "LINGUAGENS"])).Value!,
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static void DefinirClassificacao(ProcessoSeletivo processo, int variante, bool permutar) =>
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, '2'),
            casasArredondamento: 3,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '3'),
            regrasEliminacao: RegrasDeEliminacao(variante),
            baseadoEmEnem: true,
            resolucaoPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Resolucao,
            quadroPesoAreaEnem: QuadroPesoAreaEnemDeTeste.Completo(inverterOrdem: permutar), escalaNotaFinal: 100m).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static List<RegraEliminacao> RegrasDeEliminacao(int variante) =>
    [
        // DUAS do mesmo código, args distintos — o PS Convênios exige exatamente isso.
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(EtapaId(1, variante), 45.0000m)).Value!,
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(EtapaId(2, variante), 30.5000m)).Value!,
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimCorteEmArea, '5'), new ArgsElimCorteEmArea("REDACAO", 400.0000m)).Value!,
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, '6'), new ArgsElimZeroEmArea()).Value!,
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, '0'), new ArgsElimFaltaEmDiaDeProvaEnem()).Value!,
        RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, '7'), new ArgsElimAusenciaNaEtapa(EtapaId(3, variante))).Value!,
    ];

    private static void DefinirFormularioDeInscricao(ProcessoSeletivo processo, bool permutar)
    {
        DefinirFatosColetados(processo, permutar);
        PredicadoDnf corPreta = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;
        DefinirTituloETermos(processo, corPreta);
        DefinirEtapasDoFormulario(processo, corPreta);
        DefinirRegrasDeDerivacao(processo, permutar);
    }

    // Coleta de fatos + derivação (Story #928, §7.4): COR_RACA vem da seção do conjunto básico,
    // sem gate; RENDA é gatado por COR_RACA (pré-condição); a modalidade do candidato é
    // derivada das regras das modalidades ofertadas, e a das cotas de renda cita COR_RACA e
    // RENDA. Exercita, no mesmo snapshot, as arestas de produção, pré-condição e derivação,
    // com predicado DNF de duas condições numa cláusula. A seção traz também a UF do Geo, seleção com as
    // UFs congeladas, e o município, sem lista, que cita a UF. A renda acima de dez salários
    // impede a inscrição de quem se declarou preto, com a mensagem ao candidato.
    private static void DefinirFatosColetados(ProcessoSeletivo processo, bool permutar) =>
        processo.DefinirItens(Ordem([
            .. FormularioDeTeste.DadosBasicos(),
            FatoDeRenda(),
            FatoDeConclusaoDoEnsinoMedio(),
            // A classificação é baseada em ENEM: a inscrição pede a edição, entre as admitidas.
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            FatoDeOpcaoPelaEscolaPublica(),
        ], permutar), PrecondicaoIfMatch.Ausente, grupos: [ComposicaoFamiliar(permutar)]).IsSuccess.Should().BeTrue();

    private static FatoColetado FatoDeRenda() =>
        FatoColetado.Criar("RENDA", 1, "Faixa de renda familiar", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, [
            CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
        ], impedimento: new Impedimento(
            PredicadoDnf.CriarDeCondicoesAgrupadas(
            [
                (0, CondicaoDnf.Criar("RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ACIMA_10_SM")).Value!),
                (0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!),
            ]).Value!,
            "A renda declarada está acima do limite deste processo."), classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    // A faixa de data com os dois tipos de limite: uma data fixa e o dia do envio deslocado.
    private static FatoColetado FatoDeConclusaoDoEnsinoMedio() =>
        FatoColetado.Criar("CONCLUSAO_ENSINO_MEDIO", 4, "Data de conclusão do ensino médio", TipoRenderizacao.Data, Obrigatoriedade.Sempre, null,
            classificacaoProtecao: "PESSOAL",
            restricoes: [new FaixaDeData(LimiteDeData.Fixo(new DateOnly(1950, 1, 1)), LimiteDeData.NoEnvio(anos: 0, dias: -1))], hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    // A opção pelas vagas de escola pública, de que a opção pelas cotas da lei depende.
    private static FatoColetado FatoDeOpcaoPelaEscolaPublica() =>
        FatoColetado.Criar(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, 5, "Deseja concorrer às vagas de escola pública?",
            TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    // Formulário de inscrição (Story #559, UNI-REQ-0086): título e um termo com exibição e
    // obrigatoriedade condicionais — o decoder tem de remontar o termo inteiro.
    private static void DefinirTituloETermos(ProcessoSeletivo processo, PredicadoDnf corPreta)
    {
        processo.DefinirTitulo("Formulário de Inscrição — PS Rico 2026", PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermos(
            [Termo("DECLARACAO_PERTENCIMENTO", 0, exibicao: corPreta, obrigatoriedade: Obrigatoriedade.Quando(corPreta))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    // Seção com exibição condicional: aparece só para quem se declarou preto — o decoder remonta
    // a exibição da seção junto das demais etapas.
    private static void DefinirEtapasDoFormulario(ProcessoSeletivo processo, PredicadoDnf corPreta)
    {
        FormularioProcesso inscricao = processo.FormularioDe(FinalidadeFormulario.Inscricao)!;
        processo.DefinirFormulario(
            FinalidadeFormulario.Inscricao, inscricao.FaseId, inscricao.Titulo,
            [
                FormularioDeTeste.Etapas()[0],
                EtapaFormulario.Criar(FormularioDeTeste.Secao, 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
                EtapaFormulario.Criar("PERTENCIMENTO", 2, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Pertencimento", null, null, corPreta).Value!,
                EtapaFormulario.Criar("REVISAO", 3, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
            ],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    // A lista EXTERNA de configurações de derivação é ordenada por codigoFato na projeção, e a
    // de regras, pela ordem; sem dois itens em cada uma, a permutação não seria exercitada.
    // Cita COR_RACA e RENDA (coletados), sem ciclo.
    private static void DefinirRegrasDeDerivacao(ProcessoSeletivo processo, bool permutar) =>
        processo.DefinirRegrasDerivacao(Ordem([
            DerivacaoDoRegimeDeIngresso(permutar),
            DerivacaoDoEgressoDeEscolaPublica(),
            DerivacaoDoPretoDeclarado(),
        ], permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

    private static ConfiguracaoDerivacaoFato DerivacaoDoRegimeDeIngresso(bool permutar) =>
        ConfiguracaoDerivacaoFato.Criar("REGIME_INGRESSO", Ordem([
            RegraDerivacaoConfigurada.Criar(0, "AMPLA", null).Value!,
            RegraDerivacaoConfigurada.Criar(1, "COTISTA", Ordem([
                CondicaoRegraDerivacao.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
                CondicaoRegraDerivacao.Criar(0, "RENDA", Operador.Igual, JsonSerializer.SerializeToElement("ATE_1_SM")).Value!,
            ], permutar)).Value!,
        ], permutar)).Value!;

    // O egresso de escola pública, de que a opção pelas cotas da lei depende.
    private static ConfiguracaoDerivacaoFato DerivacaoDoEgressoDeEscolaPublica() =>
        ConfiguracaoDerivacaoFato.Criar(OrigemEscolar.FatoEgresso, [
            RegraDerivacaoConfigurada.Criar(0, null, [
                CondicaoRegraDerivacao.Criar(0, OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ]).Value!,
        ]).Value!;

    // Derivado booleano: as regras não contribuem código, e o envelope grava contribui nulo.
    private static ConfiguracaoDerivacaoFato DerivacaoDoPretoDeclarado() =>
        ConfiguracaoDerivacaoFato.Criar("PRETO_DECLARADO", [
            RegraDerivacaoConfigurada.Criar(0, null, [
                CondicaoRegraDerivacao.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ]).Value!,
        ]).Value!;

    // Cascata de remanejamento (Story #575): as 8 modalidades federais de
    // DistribuicaoLei12711 já são SegueCascata (INV-12) — sem uma cascata que as cubra,
    // Publicar() recusa com ProcessoSeletivo.CascataOrigemAusente (PendenciaDaCascata).
    // A matriz legal completa (8×7, fallback AC) é o que mantém este corpus PUBLICÁVEL.
    private static void DefinirCascataEDocumentos(ProcessoSeletivo processo, int variante, bool permutar, bool comArvoreSatisfacao)
    {
        processo.DefinirCascataRemanejamento(CascataLegalCompleta(permutar), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Árvore de satisfação de documentos exigidos (Story #920) — opt-in (ver o parâmetro
        // comArvoreSatisfacao acima): incluí-la incondicionalmente mudaria o envelope de
        // referência que as golden fixtures e os testes de round-trip congelam.
        if (comArvoreSatisfacao)
        {
            processo.DefinirDocumentosExigidos(ArvoreSatisfacaoRica(variante, permutar), PrecondicaoIfMatch.Ausente)
                .IsSuccess.Should().BeTrue();
        }
    }

    // Taxa de inscrição e isenção (issue #1112): o corpus RICO cobra taxa e referencia os
    // dois fundamentos — a combinação "cobra-com-fundamentos" do CA-08, que o corpus BASE
    // (sem cobrança) não cobre.
    private static void DefinirTaxaEOpcoesDeCurso(ProcessoSeletivo processo, bool permutar)
    {
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(
                cobra: true,
                valor: 150.00m,
                fundamentosCodigos: Ordem([FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea], permutar)).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        // O corpus rico oferta dois cursos e admite a segunda opção.
        processo.DefinirOpcoesDeCurso(2, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }
}
