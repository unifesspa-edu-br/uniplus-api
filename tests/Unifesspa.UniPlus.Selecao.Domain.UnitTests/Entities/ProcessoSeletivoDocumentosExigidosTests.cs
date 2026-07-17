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

    private static DocumentoExigido Exigencia(
        Guid exigidoNaFaseId, Aplicabilidade aplicabilidade = Aplicabilidade.Geral, string? consequenciaIndeferimento = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "IDENTIDADE",
            tipoDocumentoNome: "Documento de identidade",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade,
            obrigatorio: consequenciaIndeferimento is null,
            consequenciaIndeferimento,
            grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;

    private static FaseCronograma FaseComComplementacao(int ordem, string codigo, bool permiteComplementacao) => FaseCronograma.Criar(
        ordem,
        Guid.CreateVersion7(),
        codigo,
        "CEPS",
        OrigemDataFase.Delegada,
        agrupaEtapas: false,
        permiteComplementacao: permiteComplementacao,
        produzResultado: false,
        resultadoDefinitivo: false,
        coletaInscricao: false,
        inicio: null,
        fim: null,
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

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

    [Fact(DisplayName = "Story #554/issue #893 (PR-d): redefinir o cronograma preservando a Ordem de uma fase referenciada por exigência viva é aceito — reconciliação, não guard bruto")]
    public void DefinirCronogramaFases_MesmaOrdemComExigenciaViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DocumentoExigido exigencia = Exigencia(fase.Id);
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Mesma Ordem (1), dados diferentes — o caso comum de uma sessão editorial que só
        // ajusta datas: a reconciliação reusa a instância viva, preserva o Id, e a
        // exigência continua referenciando uma fase que existe.
        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO_REVISADA")], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.CronogramaFases.Should().ContainSingle(f => f.Id == fase.Id, "a reconciliação por Ordem reusa a MESMA instância, preservando o Id");
        processo.CronogramaFases.Single().Codigo.Should().Be("INSCRICAO_REVISADA");
        exigencia.ExigidoNaFaseId.Should().Be(fase.Id, "a exigência nunca deixou de referenciar uma fase existente");
    }

    [Fact(DisplayName = "CA-04: redefinir o cronograma removendo (Ordem que desaparece) uma fase referenciada por exigência viva é recusado")]
    public void DefinirCronogramaFases_RemoveOrdemReferenciadaPorExigenciaViva_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = Fase(1, "INSCRICAO");
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([Exigencia(fase.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Ordem 2 no lugar de Ordem 1 — a fase de Ordem 1 desaparece de fato.
        Result resultado = processo.DefinirCronogramaFases([Fase(2, "INSCRICAO")], [], PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir o cronograma sem exigência viva é aceito (contraprova)")]
    public void DefinirCronogramaFases_SemExigenciaViva_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-04: retirar PermiteComplementacao de fase referenciada por exigência PENDENCIA_REENVIO é recusado")]
    public void DefinirCronogramaFases_RetiraComplementacaoComPendenciaReenvio_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [Exigencia(fase.Id, Aplicabilidade.Geral, consequenciaIndeferimento: "PENDENCIA_REENVIO")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: false)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.PendenciaReenvioExigeComplementacao");
    }

    [Fact(DisplayName = "Retirar PermiteComplementacao sem exigência PENDENCIA_REENVIO é aceito (contraprova)")]
    public void DefinirCronogramaFases_RetiraComplementacaoSemPendenciaReenvio_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([Exigencia(fase.Id)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: false)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Manter PermiteComplementacao verdadeiro com exigência PENDENCIA_REENVIO é aceito (contraprova)")]
    public void DefinirCronogramaFases_MantemComplementacaoComPendenciaReenvio_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [Exigencia(fase.Id, Aplicabilidade.Geral, consequenciaIndeferimento: "PENDENCIA_REENVIO")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── Story #554/issue #893 (achado Codex P2, PR #900) — IdadeMaximaEmissao.ReferenciaFaseId ──

    private static FaseCronograma FaseComExtremo(int ordem, string codigo, DateTimeOffset? inicio, DateTimeOffset? fim) => FaseCronograma.Criar(
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
        inicio: inicio,
        fim: fim,
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static DocumentoExigido ExigenciaComIdadeAncoradaEmFase(
        Guid exigidoNaFaseId, Guid referenciaFaseId, ReferenciaTipoIdadeEmissao referenciaTipo)
    {
        IdadeMaximaEmissao idade = IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, referenciaTipo, null, referenciaFaseId).Value!;
        return DocumentoExigido.Criar(
            exigidoNaFaseId,
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "COMPROVANTE_RESIDENCIA",
            tipoDocumentoNome: "Comprovante de residência",
            tipoDocumentoCategoria: "PESSOAL",
            aplicabilidade: Aplicabilidade.Geral,
            obrigatorio: true,
            consequenciaIndeferimento: null,
            grupoSatisfacaoId: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: idade, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #900): remover fase usada só como âncora de IdadeMaximaEmissao (não ExigidoNaFaseId) é recusado")]
    public void DefinirCronogramaFases_RemoveFaseAncoraDeIdade_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DateTimeOffset fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        FaseCronograma faseExigencia = Fase(1, "INSCRICAO");
        FaseCronograma faseAncora = FaseComExtremo(2, "ANALISE", inicio: null, fim: fim);
        processo.DefinirCronogramaFases([faseExigencia, faseAncora], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [ExigenciaComIdadeAncoradaEmFase(faseExigencia.Id, faseAncora.Id, ReferenciaTipoIdadeEmissao.FimFase)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Ordem 2 (faseAncora) desaparece — só a Ordem 1 (faseExigencia, referenciada por
        // ExigidoNaFaseId) é mantida.
        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], [], PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue(
            "faseAncora não é a fase de ExigidoNaFaseId, mas é a âncora de IdadeMaximaEmissao.ReferenciaFaseId");
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Achado Codex P2 (PR #900): fase sobrevivente que perde o extremo usado como âncora de idade é recusado")]
    public void DefinirCronogramaFases_FaseSobreviventePerdeExtremoAncoraDeIdade_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DateTimeOffset fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        FaseCronograma fase = FaseComExtremo(1, "ANALISE", inicio: null, fim: fim);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [ExigenciaComIdadeAncoradaEmFase(fase.Id, fase.Id, ReferenciaTipoIdadeEmissao.FimFase)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // Mesma Ordem (1) — a fase sobrevive via reconciliação, mas perde o Fim.
        Result resultado = processo.DefinirCronogramaFases(
            [FaseComExtremo(1, "ANALISE", inicio: null, fim: null)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "Fase sobrevivente que preserva o extremo usado como âncora de idade é aceito (contraprova)")]
    public void DefinirCronogramaFases_FaseSobrevivePreservandoExtremoAncoraDeIdade_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DateTimeOffset fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        FaseCronograma fase = FaseComExtremo(1, "ANALISE", inicio: null, fim: fim);
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos(
            [ExigenciaComIdadeAncoradaEmFase(fase.Id, fase.Id, ReferenciaTipoIdadeEmissao.FimFase)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DateTimeOffset novoFim = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        Result resultado = processo.DefinirCronogramaFases(
            [FaseComExtremo(1, "ANALISE", inicio: null, fim: novoFim)], [], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message, "o Fim continua definido — só o valor mudou, o que é uma edição legítima");
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
            condicoes: [CondicaoDe("MODALIDADE", "LB_PPI")], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
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
            condicoes: [CondicaoDe("MODALIDADE", "LB_PPI")], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
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
            condicoes: [CondicaoDe("CONDICAO_ATENDIMENTO", "PCD")], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
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
            condicoes: [CondicaoDe("CONDICAO_ATENDIMENTO", "PCD")], basesLegais: [], idadeMaximaEmissao: null, formatoPermitido: null, tamanhoMaximoBytes: null).Value!;
        processo.DefinirDocumentosExigidos([exigencia], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        OfertaCondicao condicaoPcdNova = OfertaCondicao.Criar(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência");
        OfertaCondicao condicaoLactante = OfertaCondicao.Criar(Guid.CreateVersion7(), "LACTANTE", "Lactante");
        Result resultado = processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([condicaoPcdNova, condicaoLactante], [], []).Value!, PrecondicaoIfMatch.Ausente);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
