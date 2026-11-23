namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;

public sealed class ProcessoSeletivoPublicarExigenciasDocumentaisTests
{
    [Fact(DisplayName = "CA-01: publicar com exigência CONDICIONAL vazia obrigatória é bloqueado")]
    public void Publicar_CondicionalVaziaObrigatoria_Bloqueia()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaCondicionalVaziaObrigatoria(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.CondicionalVaziaDeterminaResultado");
    }

    [Fact(DisplayName = "Story #554/PR #903: publicar com exigência GERAL configurada é aceito — a guarda B-01 foi removida (issue #548)")]
    public void Publicar_ExigenciaGeralConfigurada_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaGeral(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(
            resultado.Error?.Message ?? "o bloco documentosExigidos.exigencias deixou de ser stub — nada mais bloqueia esta publicação");
    }

    [Fact(DisplayName = "Publicar sem nenhum documento exigido configurado não é afetado pela guarda (contraprova)")]
    public void Publicar_SemDocumentosExigidos_NaoBloqueiaPorExigencias()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-02: AvaliarConformidade inclui o item 'Base legal das exigências documentais'")]
    public void AvaliarConformidade_IncluiItemBaseLegal()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Should().ContainSingle(i => i.Codigo == "exigencias_base_legal_nao_resolvida");
    }

    [Fact(DisplayName = "CA-03 (semântica vazia): processo sem exigência que determina resultado tem o item satisfeito")]
    public void AvaliarConformidade_SemExigenciaQueDeterminaResultado_ItemSatisfeito()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Exigência que determina resultado sem base legal reprova o item")]
    public void AvaliarConformidade_ExigenciaSemBaseLegal_ItemReprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-02: exigência com base RESOLVIDO satisfaz o item")]
    public void AvaliarConformidade_ExigenciaComBaseResolvida_ItemSatisfeito()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!])
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "Exigência com base só PENDENTE reprova o item")]
    public void AvaliarConformidade_ExigenciaComBasePendente_ItemReprova()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Pendente)), 0).Value!])
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-05: reenviar o PUT rebaixando a única base resolvida para PENDENTE volta a reprovar o item")]
    public void AvaliarConformidade_ReenviarRebaixandoUnicaBaseResolvida_VoltaAReprovar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!])
            .IsSuccess.Should().BeTrue();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();

        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Pendente)), 0).Value!])
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "CA-05: reenviar o PUT sem a única base resolvida volta a reprovar o item")]
    public void AvaliarConformidade_ReenviarSemUnicaBaseResolvida_VoltaAReprovar()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId, BaseLegalDe(StatusBaseLegal.Resolvido)), 0).Value!])
            .IsSuccess.Should().BeTrue();
        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeTrue();

        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();

        ChecklistDeConformidade.AvaliarConformidade(processo.Declaracao(), CadastrosDeTeste.Para(processo)).Single(i => i.Codigo == "exigencias_base_legal_nao_resolvida").Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "Publicar bloqueia com ConformidadeInsuficiente quando exigência determina resultado sem base legal — antes de alcançar B-01")]
    public void Publicar_ExigenciaSemBaseLegal_BloqueiaComConformidadeInsuficiente()
    {
        ProcessoSeletivo processo = NovoProcessoConforme();
        Guid faseId = processo.CronogramaFases.Single().Id;
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaObrigatoriaCom(faseId), 0).Value!])
            .IsSuccess.Should().BeTrue();

        Result<VersaoConfiguracao> resultado = processo.Publicar(CenarioDoEdital());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
        resultado.Error.Message.Should().Contain("Base legal das exigências documentais");
    }

    private static DocumentoExigido ExigenciaCondicionalVaziaObrigatoria(Guid exigidoNaFaseId) => DocumentoExigido.Criar(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "CERTIDAO_RESERVISTA", "Certidão de reservista", "MILITAR", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(exigidoNaFaseId, null, FinalidadeFormulario.Inscricao),
        new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, []),
        new IndeferimentoDoDocumento(true, null, [BaseLegalResolvidaQualquer()]),
        new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()),
        null).Value!;

    private static DocumentoExigido ExigenciaGeral(Guid exigidoNaFaseId) => DocumentoExigido.Criar(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(exigidoNaFaseId, null, FinalidadeFormulario.Inscricao),
        new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
        new IndeferimentoDoDocumento(true, null, [BaseLegalResolvidaQualquer()]),
        new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()),
        null).Value!;

    private static DocumentoExigidoBaseLegal BaseLegalDe(StatusBaseLegal status) => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, status, null).Value!;

    private static DocumentoExigido ExigenciaObrigatoriaCom(Guid exigidoNaFaseId, params DocumentoExigidoBaseLegal[] basesLegais) => DocumentoExigido.Criar(
        new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "IDENTIDADE", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
        new MomentoDaExigencia(exigidoNaFaseId, null, FinalidadeFormulario.Inscricao),
        new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
        new IndeferimentoDoDocumento(true, null, basesLegais),
        new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()),
        null).Value!;
}
