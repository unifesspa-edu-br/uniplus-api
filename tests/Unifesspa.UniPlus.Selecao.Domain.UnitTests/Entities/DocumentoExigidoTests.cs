namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cobertura de <see cref="DocumentoExigido"/> (Story #554, PR-a): fábrica, guard de
/// coerência de aplicabilidade (CA-01) e <see cref="DocumentoExigido.DeterminaResultado"/>.
/// </summary>
public sealed class DocumentoExigidoTests
{
    private static Result<DocumentoExigido> Exigencia(
        Aplicabilidade aplicabilidade = Aplicabilidade.Geral,
        bool obrigatorio = false,
        string? consequenciaIndeferimento = null,
        IReadOnlyList<CondicaoGatilho>? condicoes = null,
        IReadOnlyList<DocumentoExigidoBaseLegal>? basesLegais = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId: Guid.CreateVersion7(),
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "LAUDO_MEDICO",
            tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE",
            aplicabilidade: aplicabilidade,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: consequenciaIndeferimento,
            grupoSatisfacaoId: null,
            condicoes: condicoes ?? [],
            basesLegais: basesLegais ?? []);

    private static CondicaoGatilho CondicaoQualquer() => CondicaoGatilho.Criar(
        0, "SEXO", Operador.Igual, JsonSerializer.SerializeToElement("MASCULINO")).Value!;

    [Fact(DisplayName = "CA-01: aplicabilidade Nenhuma é recusada")]
    public void Criar_AplicabilidadeNenhuma_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(Aplicabilidade.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.AplicabilidadeObrigatoria");
    }

    [Fact(DisplayName = "GERAL/CONDICIONAL válidos são aceitos")]
    public void Criar_ValoresValidos_Aceita()
    {
        Exigencia(Aplicabilidade.Geral).IsSuccess.Should().BeTrue();
        Exigencia(Aplicabilidade.Condicional).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Consequência de indeferimento fora do domínio é recusada")]
    public void Criar_ConsequenciaInvalida_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(consequenciaIndeferimento: "REPROVA_TUDO");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.ConsequenciaIndeferimentoInvalida");
    }

    [Theory(DisplayName = "Consequências válidas são aceitas")]
    [InlineData("ELIMINA")]
    [InlineData("RECLASSIFICA_AC")]
    [InlineData("REMOVE_VANTAGEM")]
    [InlineData("PENDENCIA_REENVIO")]
    public void Criar_ConsequenciaValida_Aceita(string consequencia) =>
        Exigencia(consequenciaIndeferimento: consequencia).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "CA-01: GERAL com condição viva é recusada")]
    public void GarantirCoerenciaAplicabilidade_GeralComCondicaoViva_Recusa()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Geral).Value!;

        DomainError? erro = exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: true);

        erro.Should().NotBeNull();
        erro!.Code.Should().Be("DocumentoExigido.GeralComCondicao");
    }

    [Fact(DisplayName = "Story #892 (PR-b): Criar recusa GERAL com condição real (não mais parâmetro sintético)")]
    public void Criar_GeralComCondicaoReal_Recusa()
    {
        Result<DocumentoExigido> resultado = Exigencia(Aplicabilidade.Geral, condicoes: [CondicaoQualquer()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.GeralComCondicao");
    }

    [Fact(DisplayName = "Story #892 (PR-b): Criar aceita CONDICIONAL com condição real e a anexa à coleção")]
    public void Criar_CondicionalComCondicaoReal_Aceita()
    {
        CondicaoGatilho condicao = CondicaoQualquer();
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Condicional, condicoes: [condicao]).Value!;

        exigencia.Condicoes.Should().ContainSingle(c => c.Id == condicao.Id);
        condicao.DocumentoExigidoId.Should().Be(exigencia.Id);
    }

    [Fact(DisplayName = "CA-01: GERAL sem condição viva é coerente (contraprova)")]
    public void GarantirCoerenciaAplicabilidade_GeralSemCondicaoViva_Aceita()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Geral).Value!;

        exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: false).Should().BeNull();
    }

    [Fact(DisplayName = "CA-01: CONDICIONAL com condição viva é coerente (contraprova)")]
    public void GarantirCoerenciaAplicabilidade_CondicionalComCondicaoViva_Aceita()
    {
        DocumentoExigido exigencia = Exigencia(Aplicabilidade.Condicional).Value!;

        exigencia.GarantirCoerenciaAplicabilidade(possuiCondicaoViva: true).Should().BeNull();
    }

    [Fact(DisplayName = "DeterminaResultado: obrigatória determina resultado")]
    public void DeterminaResultado_Obrigatoria_RetornaTrue() =>
        Exigencia(obrigatorio: true).Value!.DeterminaResultado().Should().BeTrue();

    [Fact(DisplayName = "DeterminaResultado: com consequência determina resultado")]
    public void DeterminaResultado_ComConsequencia_RetornaTrue() =>
        Exigencia(consequenciaIndeferimento: "ELIMINA").Value!.DeterminaResultado().Should().BeTrue();

    [Fact(DisplayName = "DeterminaResultado: facultativa e sem consequência não determina resultado (contraprova)")]
    public void DeterminaResultado_FacultativaSemConsequencia_RetornaFalse() =>
        Exigencia().Value!.DeterminaResultado().Should().BeFalse();

    [Fact(DisplayName = "Consequência composta só de espaços é normalizada para null (não determina resultado)")]
    public void Criar_ConsequenciaSoDeEspacos_NormalizaParaNull()
    {
        DocumentoExigido exigencia = Exigencia(consequenciaIndeferimento: "   ").Value!;

        exigencia.ConsequenciaIndeferimento.Should().BeNull();
        exigencia.DeterminaResultado().Should().BeFalse();
    }

    // ── Story #554/issue #549 (PR-c) — base legal 1:N ──

    [Fact(DisplayName = "CA-06: BasesLegaisResolvidas exclui toda base PENDENTE")]
    public void BasesLegaisResolvidas_ExcluiPendente()
    {
        DocumentoExigidoBaseLegal pendente = DocumentoExigidoBaseLegal.Criar(
            "Referência pendente", TipoAbrangencia.Federal, StatusBaseLegal.Pendente, null).Value!;
        DocumentoExigidoBaseLegal resolvida = DocumentoExigidoBaseLegal.Criar(
            "Referência resolvida", TipoAbrangencia.Estadual, StatusBaseLegal.Resolvido, null).Value!;
        DocumentoExigido exigencia = Exigencia(basesLegais: [pendente, resolvida]).Value!;

        exigencia.BasesLegaisResolvidas().Should().ContainSingle().Which.Should().Be(resolvida);
    }

    [Fact(DisplayName = "BasesLegaisResolvidas retorna vazio quando só há PENDENTE (contraprova)")]
    public void BasesLegaisResolvidas_SoPendente_RetornaVazio()
    {
        DocumentoExigidoBaseLegal pendente = DocumentoExigidoBaseLegal.Criar(
            "Referência pendente", TipoAbrangencia.Federal, StatusBaseLegal.Pendente, null).Value!;
        DocumentoExigido exigencia = Exigencia(basesLegais: [pendente]).Value!;

        exigencia.BasesLegaisResolvidas().Should().BeEmpty();
    }
}
