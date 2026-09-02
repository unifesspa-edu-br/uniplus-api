namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura das 7 variantes de <see cref="PredicadoObrigatoriedade"/> avaliadas por
/// <see cref="AvaliadorConformidadeLegal"/> (Story #853, §3.1/CA-01 a CA-09) contra o
/// agregado <see cref="ProcessoSeletivo"/> real. <c>BonusObrigatorio</c> (CA-05, oitava
/// variante original) foi descartada — ADR-0114, executado por esta story:
/// <c>ConfiguracaoBonusRegional</c> é global ao processo, sem lista de modalidades.
/// </summary>
public sealed class AvaliadorConformidadeLegalTests
{
    private const string TipoProcessoAvaliado = "SiSU";

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Avaliador 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

    private static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

    [Fact(DisplayName = "CA-01: sem nenhuma regra vigente, a avaliação aprova (lista vazia)")]
    public void SemRegras_Aprova()
    {
        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [], IdentidadesDeCadastro.Vazio);

        resultado.Regras.Should().BeEmpty();
        resultado.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CA-01: devolve exatamente uma RegraAvaliada por ObrigatoriedadeLegal de entrada")]
    public void UmaRegraAvaliadaPorRegraDeEntrada()
    {
        ObrigatoriedadeLegal regra1 = NovaRegra("R1", new ConcorrenciaDuplaObrigatoria());
        ObrigatoriedadeLegal regra2 = NovaRegra("R2", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [regra1, regra2], IdentidadesDeCadastro.Vazio);

        resultado.Regras.Should().HaveCount(2);
        resultado.Regras.Select(r => r.RegraCodigo).Should().BeEquivalentTo(["R1", "R2"]);
        resultado.Regras.Should().OnlyContain(r => r.TipoProcessoCodigoAvaliado == TipoProcessoAvaliado);
    }

    private static TipoEtapaSnapshot NovoTipoEtapa(string codigo, string nome) =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), codigo, nome).Value!;

    [Fact(DisplayName = "issue #1071 — cenário 1: nome editorial não interfere na conformidade")]
    public void EtapaObrigatoria_ComTipoCongeladoCorrespondente_AprovaIndependenteDoNome()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "Primeira avaliação", CaraterEtapa.Classificatoria, NovoTipoEtapa("PROVA_OBJETIVA", "Prova Objetiva"),
                peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "o rótulo editorial ('Primeira avaliação') não participa da avaliação — só o código congelado do tipo");
        resultado.Regras.Single().Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");
    }

    [Fact(DisplayName = "issue #1071 — cenário 2: nome igual ao código não substitui a identidade do tipo")]
    public void EtapaObrigatoria_MesmoNomeTipoDiferente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "PROVA_OBJETIVA", CaraterEtapa.Classificatoria, NovoTipoEtapa("ENTREVISTA", "Entrevista"),
                peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "a etapa se chama 'PROVA_OBJETIVA' mas o tipo congelado é ENTREVISTA — nome não é identidade");
        avaliada.Motivo.Should().Contain("PROVA_OBJETIVA",
            "CA exige que a reprovação nomeie o código do tipo ausente, não só um booleano");
    }

    [Fact(DisplayName = "CA (EtapaObrigatoria): reprova nomeando o código do tipo ausente")]
    public void EtapaObrigatoria_SemEtapaCorrespondente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "Redação", CaraterEtapa.Classificatoria, NovoTipoEtapa("REDACAO", "Redação"),
                peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("PROVA_OBJETIVA",
            "a reprovação precisa nomear o código do tipo ausente, não só um booleano");
    }

    /// <remarks>
    /// A identidade é parâmetro porque duas ofertas que selecionam a mesma modalidade
    /// referenciam a mesma linha do cadastro — e é a identidade que a avaliação compara.
    /// </remarks>
    private static ModalidadeSelecionada NovaModalidade(
        string codigo,
        NaturezaLegalModalidade natureza,
        Guid? origemId = null) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: origemId ?? Guid.CreateVersion7(),
            codigo: codigo,
            descricao: null,
            naturezaLegal: natureza,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: natureza == NaturezaLegalModalidade.CotaReservada
                ? RegraRemanejamentoModalidade.SegueCascata
                : RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Lei 12.711/2012").Value!;

    private static ConfiguracaoDistribuicaoVagas NovaOferta(params ModalidadeSelecionada[] modalidades) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 0.6m,
            regraDistribuicao: ReferenciaRegra.Criar(
                "DISTRIBUICAO-PADRAO", "v1", new string('a', 64)).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: modalidades).Value!;

    [Fact(DisplayName = "CA-03 (ModalidadesMinimas): aprova sse TODA oferta contém todas as modalidades exigidas")]
    public void ModalidadesMinimas_TodasAsOfertasTemAModalidade_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ampla = Guid.CreateVersion7();
        Guid cota = Guid.CreateVersion7();
        processo.DefinirDistribuicaoVagas(
            [
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla, ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, cota)),
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla, ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, cota)),
            ],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-03 (ModalidadesMinimas — contraprova obrigatória): reprova nomeando a oferta que falhou")]
    public void ModalidadesMinimas_OfertaSemModalidade_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaCompleta = NovaOferta(
            NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada));
        ConfiguracaoDistribuicaoVagas ofertaQueFalha = NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla));
        processo.DefinirDistribuicaoVagas(
            [ofertaCompleta, ofertaQueFalha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "a Lei 12.711 reserva vagas por curso e turno — uma oferta sem a modalidade é ilegal ainda que outra a tenha");
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain(ofertaQueFalha.Id.ToString(),
            "CA-03 exige que o erro nomeie A OFERTA que falhou (não só o código da modalidade ausente) — " +
            "e não a oferta completa, que não tem nada a ver com a reprovação");
    }

    [Fact(DisplayName = "CA-04 (DesempateDeveIncluir): aprova por código de catálogo, não pelo rótulo")]
    public void DesempateDeveIncluir_PorCodigoDeCatalogo_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ReferenciaRegra regraDesempate = ReferenciaRegra.Criar("DESEMPATE-IDOSO", "v1", new string('b', 64)).Value!;
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, regraDesempate, new ArgsDesempateIdoso(IdadeMinima: 60)).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("DESEMPATE", new DesempateDeveIncluir("DESEMPATE-IDOSO"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "AtendimentoDisponivel: renomear o tipo de deficiência não quebra a regra escrita antes")]
    public void AtendimentoDisponivel_AposRenomearOTipo_ContinuaAprovando()
    {
        // O defeito que este teste fecha: a avaliação comparava pelo NOME do tipo
        // congelado na oferta. Renomear "Deficiência auditiva" para "Auditiva" — mudança
        // cosmética e legítima no cadastro — fazia toda regra escrita antes deixar de
        // casar, e a cláusula legal passava a reprovar processos conformes.
        //
        // Com a comparação pelo código, o rótulo pode mudar à vontade: a identidade que
        // a regra cita é a mesma.
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "AUDITIVA", "Nome completamente diferente do que a regra cita")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "a regra cita o código, e o código não mudou — só o rótulo de exibição");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: o nome ofertado não satisfaz a regra que cita o código")]
    public void AtendimentoDisponivel_ComNomeNoLugarDoCodigo_Reprova()
    {
        // A contraprova do teste acima: se a comparação ainda caísse no nome, este
        // cenário aprovaria — o nome bate, o código não.
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "DEFICIENCIA_AUDITIVA", "AUDITIVA")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeFalse(
            "o tipo ofertado tem código DEFICIENCIA_AUDITIVA; AUDITIVA é apenas o nome dele");
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): aprova quando todo código exigido está entre os tipos ofertados")]
    public void AtendimentoDisponivel_ComNecessidadeOfertada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "AUDITIVA", "auditiva")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): reprova SEM lançar quando OfertaAtendimento é nula")]
    public void AtendimentoDisponivel_OfertaNula_ReprovaSemLancar()
    {
        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        Action act = () =>
        {
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra], IdentidadesDeCadastro.Vazio);
            RegraAvaliada avaliada = resultado.Regras.Single();
            avaliada.Aprovada.Should().BeFalse();
            avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
        };

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria): aprova quando há modalidade CotaReservada")]
    public void ConcorrenciaDupla_ComCotaReservada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria — contraprova obrigatória, NÃO é tautológica): REPROVA sem cota reservada")]
    public void ConcorrenciaDupla_SemCotaReservada_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla))],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "um processo só de ampla concorrência é legítimo (uma transferência pode não ter cota nenhuma), " +
            "e a regra só é cadastrada para os tipos em que a lei obriga a concorrência dupla — um teste que " +
            "desse este predicado como sempre-aprovado estaria errado");
        avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "CA-08 (Customizado): sempre aprova, sempre emite aviso, nunca lança — inclusive com Parametros malformado")]
    public void Customizado_SempreAprovaEEmiteAviso_MesmoComParametrosMalformado()
    {
        using JsonDocument documento = JsonDocument.Parse("""{"qualquer": "coisa", "ate": [1,2,3]}""");
        ObrigatoriedadeLegal regra = NovaRegra("CUSTOM", new Customizado(documento.RootElement.Clone()));

        Action act = () =>
        {
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra], IdentidadesDeCadastro.Vazio);
            resultado.Regras.Single().Aprovada.Should().BeTrue();
            resultado.Avisos.Should().ContainSingle();
        };

        act.Should().NotThrow();
    }

    [Fact(DisplayName = "Renomear o tipo e atualizar a regra continua aprovando — a identidade não mudou")]
    public void DocumentoObrigatorio_AposRenomearOTipoEAtualizarARegra_Aprova()
    {
        // O desfecho de quem faz o certo: renomeia o tipo no cadastro e atualiza a regra
        // para o código novo. A exigência congelou o código velho, mas aponta para o
        // mesmo documento. Casando por código, o gate recusava publicação legítima com
        // uma mensagem que mandava procurar uma exigência que já estava na tela.
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        Guid identidadeDoLaudo = Guid.CreateVersion7();
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralDe(faseId, identidadeDoLaudo, "LAUDO_MEDICO"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO_PCD"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposDocumento = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["LAUDO_MEDICO_PCD"] = identidadeDoLaudo },
            });

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "a exigência designa o mesmo documento que a regra exige — só o rótulo mudou");
    }

    [Fact(DisplayName = "Código reatribuído a outro documento reprova, mesmo com a string batendo")]
    public void DocumentoObrigatorio_ComCodigoReatribuido_Reprova()
    {
        // O inverso: o código continua o mesmo, mas passou a designar outro documento.
        // Casando por string, o edital era publicado como conforme, satisfeito pelo
        // documento errado — e o snapshot congelava a exigência errada no envelope.
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralDe(faseId, Guid.CreateVersion7(), "LAUDO_MEDICO"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposDocumento = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["LAUDO_MEDICO"] = Guid.CreateVersion7() },
            });

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("designa outro documento",
            "a mensagem precisa dizer o que houve — mandar procurar exigência ausente faria o editor caçar o que está na tela");
    }

    [Fact(DisplayName = "Sem exigência alguma, a mensagem continua sendo a de cobertura ausente")]
    public void DocumentoObrigatorio_SemExigencia_MantemMotivoDeCoberturaAusente()
    {
        ProcessoSeletivo processo = NovoProcesso();
        PrepararProcessoComModalidade(processo, "LB_PPI");

        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposDocumento = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["LAUDO_MEDICO"] = Guid.CreateVersion7() },
            });

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("cobre incondicionalmente");
        avaliada.Motivo.Should().NotContain("designa outro documento");
    }

    [Fact(DisplayName = "EtapaObrigatoria: renomear o código do tipo e atualizar a regra continua aprovando")]
    public void EtapaObrigatoria_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid identidadeDoTipo = Guid.CreateVersion7();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "Prova", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(identidadeDoTipo, "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA_V2"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposEtapa = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["PROVA_OBJETIVA_V2"] = identidadeDoTipo },
            });

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "a etapa é do mesmo tipo que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "EtapaObrigatoria: código reatribuído a outro tipo reprova, mesmo com a string batendo")]
    public void EtapaObrigatoria_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "Prova", CaraterEtapa.Classificatoria, NovoTipoEtapa("PROVA_OBJETIVA", "Prova Objetiva"),
                peso: 1m, ordem: 1).Value!],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposEtapa = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["PROVA_OBJETIVA"] = Guid.CreateVersion7() },
            });

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("o código foi reatribuído",
            "a mensagem precisa dizer o que houve — mandar procurar etapa ausente faria o editor caçar o que está na tela");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: renomear o código do tipo e atualizar a regra continua aprovando")]
    public void AtendimentoDisponivel_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid identidadeDoTipo = Guid.CreateVersion7();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(identidadeDoTipo, "AUDITIVA", "Deficiência auditiva")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["DEF_AUDITIVA"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposDeficiencia = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["DEF_AUDITIVA"] = identidadeDoTipo },
            });

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "a oferta é do mesmo tipo de deficiência que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: código reatribuído a outro tipo reprova, mesmo com a string batendo")]
    public void AtendimentoDisponivel_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "AUDITIVA", "Deficiência auditiva")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                TiposDeficiencia = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["AUDITIVA"] = Guid.CreateVersion7() },
            });

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "o atendimento que carrega o código pertence a outro tipo de deficiência");
        avaliada.Motivo.Should().Contain("o código foi reatribuído");
    }

    [Fact(DisplayName = "ModalidadesMinimas: renomear o código da modalidade e atualizar a regra continua aprovando")]
    public void ModalidadesMinimas_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ModalidadeSelecionada modalidade = NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada);
        processo.DefinirDistribuicaoVagas([NovaOferta(modalidade)], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MINIMAS", new ModalidadesMinimas(["LB_PPI_V2"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                Modalidades = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["LB_PPI_V2"] = modalidade.ModalidadeOrigemId },
            });

        resultado.Regras.Single().Aprovada.Should().BeTrue(
            "a oferta contém a mesma modalidade que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "ModalidadesMinimas: código reatribuído a outra modalidade reprova, mesmo com a string batendo")]
    public void ModalidadesMinimas_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada))],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MINIMAS", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            processo, TipoProcessoAvaliado, [regra],
            IdentidadesDe(processo) with
            {
                Modalidades = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["LB_PPI"] = Guid.CreateVersion7() },
            });

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse(
            "a modalidade ofertada com esse código é outra — a exigência mínima não está cumprida");
        avaliada.Motivo.Should().Contain("o código foi reatribuído");
    }

    /// <summary>Exigência geral com a identidade do tipo escolhida pelo cenário.</summary>
    private static DocumentoExigido ExigenciaGeralDe(Guid exigidoNaFaseId, Guid tipoDocumentoOrigemId, string tipoDocumentoCodigo) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId, tipoDocumentoOrigemId, tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    // ── CA-09 (DocumentoObrigatorioParaModalidade) — Story #554, PR #903, issue #548: gate
    // real, substitui a reprovação conservadora que vigorava enquanto a guarda B-01
    // bloqueava qualquer publicação com DocumentoExigido configurado. ──

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId, string tipoDocumentoCodigo) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId, Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    private static DocumentoExigido ExigenciaCondicionalPorModalidade(
        Guid exigidoNaFaseId, string tipoDocumentoCodigo, string modalidadeCodigo, string? fatoExtra = null)
    {
        List<CondicaoGatilho> condicoes =
        [
            CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidadeCodigo)).Value!,
        ];
        if (fatoExtra is not null)
        {
            condicoes.Add(CondicaoGatilho.Criar(0, fatoExtra, Operador.Igual, JsonSerializer.SerializeToElement("QUALQUER")).Value!);
        }

        return DocumentoExigido.Criar(
            exigidoNaFaseId, Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
            condicoes: condicoes, basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;
    }

    private static Guid PrepararProcessoComModalidade(ProcessoSeletivo processo, string modalidadeCodigo)
    {
        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "ENVIO_DOCUMENTOS", "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas: false, permiteComplementacao: false, produzResultado: false, resultadoDefinitivo: false,
            coletaInscricao: false, inicio: null, fim: null, atoProduzidoCodigo: null,
            atoProduzidoEfeitoIrreversivel: false, bancasRequeridas: [], regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas(
            [NovaOferta(NovaModalidade(modalidadeCodigo, NaturezaLegalModalidade.CotaReservada))],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();

        return fase.Id;
    }

    [Fact(DisplayName = "CA-09: modalidade não ofertada por nenhuma oferta do processo aprova vazio — nada a exigir")]
    public void DocumentoObrigatorioParaModalidade_ModalidadeNaoOfertada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: modalidade ofertada sem NENHUMA exigência do tipo pedido reprova, nomeando modalidade e tipo")]
    public void DocumentoObrigatorioParaModalidade_SemExigenciaDoTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        PrepararProcessoComModalidade(processo, "LB_PPI");
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain("COMPROVANTE_RESIDENCIA",
            "CA-09 exige que a reprovação nomeie a modalidade e o tipo de documento, não só um booleano");
    }

    [Fact(DisplayName = "CA-09: exigência GERAL do tipo pedido aprova — cobre qualquer modalidade, por definição")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaGeralDoTipo_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeral(faseId, "COMPROVANTE_RESIDENCIA"), 0).Value!], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaGeralOpcional(Guid exigidoNaFaseId, string tipoDocumentoCodigo) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId, Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-09: exigência do tipo pedido que NÃO determina resultado (opcional, sem consequência) reprova — achado de revisão da PR #903")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaOpcionalDoTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaGeralOpcional(faseId, "COMPROVANTE_RESIDENCIA"), 0).Value!], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeFalse(
            "uma exigência que não determina resultado (Obrigatorio=false, sem ConsequenciaIndeferimento) é " +
            "meramente opcional — não satisfaz a obrigação legal \"a modalidade X DEVE exigir o documento Y\"");
    }

    [Fact(DisplayName = "CA-09: exigência CONDICIONAL com gatilho MODALIDADE = X (só esse fato) aprova — cobre a modalidade incondicionalmente")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaCondicionalSoPelaModalidade_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidade(faseId, "COMPROVANTE_RESIDENCIA", "LB_PPI"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: exigência CONDICIONAL cujo gatilho também depende de outro fato reprova — a cobertura da modalidade não é incondicional")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaCondicionalComFatoExtra_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidade(faseId, "COMPROVANTE_RESIDENCIA", "LB_PPI", fatoExtra: "FAIXA_ETARIA"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeFalse(
            "a exigência só cobre quem também satisfaz FAIXA_ETARIA — nem todo candidato de LB_PPI seria coberto, " +
            "e a obrigação legal (\"a modalidade X DEVE exigir o documento Y\") não admite exceção");
    }

    [Fact(DisplayName = "Story #916: exigência CONDICIONAL cujo gatilho é Indeterminado (fato extra não resolvido pelo fato sintético) não conta como cobertura incondicional — Indeterminado reprova, igual a Falso")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaComGatilhoIndeterminado_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        // AvaliadorConformidadeLegal avalia contra um fato sintético só com MODALIDADE — o
        // fato FAIXA_ETARIA (fatoExtra) nunca está presente, e por isso o gatilho avalia
        // Ternario.Indeterminado (Story #916, fail-closed), não mais Ternario.Falso — a
        // conclusão de cobertura, porém, é a mesma: não prova incondicionalidade.
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidade(faseId, "COMPROVANTE_RESIDENCIA", "LB_PPI", fatoExtra: "FAIXA_ETARIA"), 0).Value!],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeFalse(
            "Indeterminado conta como \"não provado\", mesma conclusão que Falso já dava — nenhuma mudança de comportamento observável");
    }

    [Fact(DisplayName = "CA-09: exigência do TIPO ERRADO não conta, mesmo cobrindo a modalidade corretamente")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaDeOutroTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(ExigenciaCondicionalPorModalidade(faseId, "LAUDO_MEDICO", "LB_PPI"), 0).Value!], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra], IdentidadesDe(processo));

        resultado.Regras.Single().Aprovada.Should().BeFalse();
    }

    /// <summary>
    /// Mapa de identidade derivado das próprias exigências do processo — o estado normal,
    /// em que nada foi renomeado nem reciclado: cada código designa o tipo que a exigência
    /// congelou. Os cenários de divergência montam o mapa à mão, que é o ponto deles.
    /// </summary>
    /// <summary>
    /// Cadastro vivo deduzido do próprio processo: cada código resolve para a identidade
    /// que o processo congelou. É o cenário do caminho feliz — regra e processo apontando
    /// para o mesmo item. Os testes de reciclagem sobrescrevem a entrada que interessa.
    /// </summary>
    private static IdentidadesDeCadastro IdentidadesDe(ProcessoSeletivo processo) =>
        new(
            MapaDe(processo.DocumentosExigidos, e => e.TipoDocumentoCodigo, e => e.TipoDocumentoOrigemId),
            MapaDe(
                processo.DistribuicaoVagas.SelectMany(d => d.Modalidades),
                m => m.Codigo,
                m => m.ModalidadeOrigemId),
            MapaDe(processo.Etapas, e => e.TipoEtapa.Codigo, e => e.TipoEtapa.OrigemId),
            MapaDe(
                processo.OfertaAtendimento?.TiposDeficiencia ?? [],
                t => t.TipoDeficienciaCodigo,
                t => t.TipoDeficienciaOrigemId));

    private static Dictionary<string, Guid> MapaDe<T>(
        IEnumerable<T> itens,
        Func<T, string> codigoDe,
        Func<T, Guid> origemDe) =>
        itens
            .GroupBy(codigoDe, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => origemDe(g.First()), StringComparer.Ordinal);

}
