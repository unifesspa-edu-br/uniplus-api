namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/> (Story #554,
/// PR-a): substituição integral e pertencimento da fase ao cronograma do MESMO
/// processo (§2 da issue #547).
/// </summary>
public sealed class ProcessoSeletivoDocumentosExigidosTests
{
    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Documentos Exigidos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static FaseCronograma Fase(int ordem, string codigo) => FaseCronograma.Criar(
        ordem,
        Guid.CreateVersion7(),
        codigo,
        "CEPS",
        OrigemDataFase.Delegada,
        agrupaEtapas: false,
        permiteComplementacao: false,
        produzResultado: false,
        resultadoDefinitivo: false,
        coletaInscricao: false,
        inicio: null,
        fim: null,
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static DocumentoExigido Exigencia(Guid exigidoNaFaseId, Aplicabilidade aplicabilidade = Aplicabilidade.Geral) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade,
            obrigatorio: false,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [], basesLegais: []).Value!;

    [Fact(DisplayName = "Fase que não pertence ao cronograma do processo é recusada")]
    public void DefinirDocumentosExigidos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DocumentoExigido exigencia = Exigencia(Guid.CreateVersion7());

        Result resultado = processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "Fase viva do cronograma do próprio processo é aceita")]
    public void DefinirDocumentosExigidos_FaseDoProprioProcesso_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = Exigencia(fase.Id);
        Result resultado = processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == exigencia.Id);
        exigencia.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "Coleção vazia é um estado válido — nenhuma exigência configurada")]
    public void DefinirDocumentosExigidos_ListaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirDocumentosExigidos([], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "PUT idempotente: reenviar o mesmo payload substitui a coleção por inteiro, sem duplicar")]
    public void DefinirDocumentosExigidos_ReenviarMesmoPayload_NaoDuplica()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido primeiraChamada = Exigencia(fase.Id);
        processo.DefinirDocumentosExigidos([primeiraChamada], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido segundaChamada = Exigencia(fase.Id);
        Result resultado = processo.DefinirDocumentosExigidos([segundaChamada], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(1);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == segundaChamada.Id);
    }

    [Fact(DisplayName = "CA-04 (guard parcial): redefinir o cronograma com exigência viva configurada é recusado")]
    public void DefinirCronogramaFases_ComExigenciaViva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([Exigencia(fase.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "toda chamada aqui recria as fases com Id novo — sem a guarda, a FK Restrict de " +
            "documentos_exigidos.exigido_na_fase_id estouraria DbUpdateException não tratada");
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir o cronograma sem exigência viva é aceito (contraprova)")]
    public void DefinirCronogramaFases_SemExigenciaViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── Story #554/issue #892 (achado Codex P2, PR #896) — CA-03 backward guard ──

    private static CondicaoGatilho CondicaoDe(string fato, string valor) => CondicaoGatilho.Criar(
        0, fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    private static ModalidadeSelecionada Modalidade(string codigo) => ModalidadeSelecionada.Criar(
        modalidadeOrigemId: Guid.CreateVersion7(),
        codigo: codigo,
        descricao: null,
        naturezaLegal: NaturezaLegalModalidade.Ampla,
        composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
        composicaoOrigemCodigo: null,
        regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
        remanejamentoDestino: null,
        remanejamentoPar: null,
        remanejamentoFallback: null,
        criteriosCumulativos: [],
        acaoQuandoIndeferido: null,
        baseLegal: "Res. Unifesspa 532/2021",
        quantidadeDeclarada: 10).Value!;

    private static ConfiguracaoDistribuicaoVagas DistribuicaoCom(params string[] codigosModalidade)
    {
        ReferenciaRegra regra = ReferenciaRegra.Criar(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", string.Concat(Enumerable.Repeat("ab01234567", 7))[..64]).Value!;
        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 10,
            pr: 1m,
            regraDistribuicao: regra,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [.. codigosModalidade.Select(Modalidade)]).Value!;
    }

    [Fact(DisplayName = "Redefinir distribuição de vagas removendo código de modalidade referenciado por gatilho vivo é recusado")]
    public void DefinirDistribuicaoVagas_RemoveModalidadeReferenciada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoCom("LB_PPI", "AC")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [CondicaoDe("MODALIDADE", "LB_PPI")], basesLegais: []).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirDistribuicaoVagas([DistribuicaoCom("AC")], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue("LB_PPI é referenciada por uma condição de gatilho viva e deixaria de ser ofertada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir distribuição de vagas preservando o código de modalidade referenciado é aceito (contraprova)")]
    public void DefinirDistribuicaoVagas_PreservaModalidadeReferenciada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoCom("LB_PPI", "AC")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [CondicaoDe("MODALIDADE", "LB_PPI")], basesLegais: []).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirDistribuicaoVagas([DistribuicaoCom("LB_PPI", "AC", "LI_PPI")], PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Redefinir oferta de atendimento removendo código de condição referenciado por gatilho vivo é recusado")]
    public void DefinirOfertaAtendimento_RemoveCondicaoReferenciada_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [CondicaoDe("CONDICAO_ATENDIMENTO", "PCD")], basesLegais: []).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue("PCD é referenciada por uma condição de gatilho viva e deixaria de ser ofertada");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir oferta de atendimento preservando o código de condição referenciado é aceito (contraprova)")]
    public void DefinirOfertaAtendimento_PreservaCondicaoReferenciada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([condicaoPcd], [], []).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE",
            Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null, grupoSatisfacaoId: null,
            condicoes: [CondicaoDe("CONDICAO_ATENDIMENTO", "PCD")], basesLegais: []).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        OfertaCondicao condicaoPcdNova = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaCondicao condicaoLactante = OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante");
        Result resultado = processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([condicaoPcdNova, condicaoLactante], [], []).Value!, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
