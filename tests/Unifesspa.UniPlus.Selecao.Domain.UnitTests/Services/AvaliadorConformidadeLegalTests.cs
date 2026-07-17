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
        ProcessoSeletivo.Criar("PS Avaliador 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

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
            NovoProcesso(), TipoProcessoAvaliado, []);

        resultado.Regras.Should().BeEmpty();
        resultado.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "CA-01: devolve exatamente uma RegraAvaliada por ObrigatoriedadeLegal de entrada")]
    public void UmaRegraAvaliadaPorRegraDeEntrada()
    {
        ObrigatoriedadeLegal regra1 = NovaRegra("R1", new ConcorrenciaDuplaObrigatoria());
        ObrigatoriedadeLegal regra2 = NovaRegra("R2", new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(
            NovoProcesso(), TipoProcessoAvaliado, [regra1, regra2]);

        resultado.Regras.Should().HaveCount(2);
        resultado.Regras.Select(r => r.RegraCodigo).Should().BeEquivalentTo(["R1", "R2"]);
        resultado.Regras.Should().OnlyContain(r => r.TipoProcessoCodigoAvaliado == TipoProcessoAvaliado);
    }

    [Fact(DisplayName = "CA-02 (EtapaObrigatoria): aprova quando existe etapa cujo Nome bate, ordinal case-insensitive")]
    public void EtapaObrigatoria_ComEtapaCorrespondente_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("prova objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("Prova Objetiva"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
        resultado.Regras.Single().Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");
    }

    [Fact(DisplayName = "CA-02 (EtapaObrigatoria): reprova nomeando o código da etapa ausente")]
    public void EtapaObrigatoria_SemEtapaCorrespondente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ETAPA", new EtapaObrigatoria("Prova Objetiva"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        RegraAvaliada avaliada = resultado.Regras.Single();
        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("Prova Objetiva",
            "CA-02 exige que a reprovação nomeie o código da etapa ausente, não só um booleano");
    }

    private static ModalidadeSelecionada NovaModalidade(string codigo, NaturezaLegalModalidade natureza) =>
        ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
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
        processo.DefinirDistribuicaoVagas(
            [
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)),
                NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)),
            ],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): aprova quando todas as necessidades aparecem entre os nomes ofertados (NFC, case-insensitive)")]
    public void AtendimentoDisponivel_ComNecessidadeOfertada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar(
                [OfertaCondicao.Criar(Guid.CreateVersion7(), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência")],
                [],
                [OfertaTipoDeficiencia.Criar(Guid.CreateVersion7(), "auditiva")]).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["Auditiva"]));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): reprova SEM lançar quando OfertaAtendimento é nula")]
    public void AtendimentoDisponivel_OfertaNula_ReprovaSemLancar()
    {
        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["Auditiva"]));

        Action act = () =>
        {
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra]);
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

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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
            ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(NovoProcesso(), TipoProcessoAvaliado, [regra]);
            resultado.Regras.Single().Aprovada.Should().BeTrue();
            resultado.Avisos.Should().ContainSingle();
        };

        act.Should().NotThrow();
    }

    // ── CA-09 (DocumentoObrigatorioParaModalidade) — Story #554, PR #903, issue #548: gate
    // real, substitui a reprovação conservadora que vigorava enquanto a guarda B-01
    // bloqueava qualquer publicação com DocumentoExigido configurado. ──

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId, string tipoDocumentoCodigo) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId, Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Geral, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;

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
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: condicoes, basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
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

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: modalidade ofertada sem NENHUMA exigência do tipo pedido reprova, nomeando modalidade e tipo")]
    public void DocumentoObrigatorioParaModalidade_SemExigenciaDoTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        PrepararProcessoComModalidade(processo, "LB_PPI");
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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
            [ExigenciaGeral(faseId, "COMPROVANTE_RESIDENCIA")], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaGeralOpcional(Guid exigidoNaFaseId, string tipoDocumentoCodigo) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId, Guid.CreateVersion7(), tipoDocumentoCodigo, "Documento de teste", "CATEGORIA",
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;

    [Fact(DisplayName = "CA-09: exigência do tipo pedido que NÃO determina resultado (opcional, sem consequência) reprova — achado de revisão da PR #903")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaOpcionalDoTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [ExigenciaGeralOpcional(faseId, "COMPROVANTE_RESIDENCIA")], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

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
            [ExigenciaCondicionalPorModalidade(faseId, "COMPROVANTE_RESIDENCIA", "LB_PPI")], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-09: exigência CONDICIONAL cujo gatilho também depende de outro fato reprova — a cobertura da modalidade não é incondicional")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaCondicionalComFatoExtra_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [ExigenciaCondicionalPorModalidade(faseId, "COMPROVANTE_RESIDENCIA", "LB_PPI", fatoExtra: "FAIXA_ETARIA")],
            PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeFalse(
            "a exigência só cobre quem também satisfaz FAIXA_ETARIA — nem todo candidato de LB_PPI seria coberto, " +
            "e a obrigação legal (\"a modalidade X DEVE exigir o documento Y\") não admite exceção");
    }

    [Fact(DisplayName = "CA-09: exigência do TIPO ERRADO não conta, mesmo cobrindo a modalidade corretamente")]
    public void DocumentoObrigatorioParaModalidade_ExigenciaDeOutroTipo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseId = PrepararProcessoComModalidade(processo, "LB_PPI");
        processo.DefinirDocumentosExigidos(
            [ExigenciaCondicionalPorModalidade(faseId, "LAUDO_MEDICO", "LB_PPI")], PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = NovaRegra(
            "DOCUMENTO", new DocumentoObrigatorioParaModalidade("LB_PPI", "COMPROVANTE_RESIDENCIA"));

        ResultadoConformidade resultado = AvaliadorConformidadeLegal.Avaliar(processo, TipoProcessoAvaliado, [regra]);

        resultado.Regras.Single().Aprovada.Should().BeFalse();
    }
}
