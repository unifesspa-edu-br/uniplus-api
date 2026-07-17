namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

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
        string? consequenciaIndeferimento = null) =>
        DocumentoExigido.Criar(
            exigidoNaFaseId: Guid.CreateVersion7(),
            tipoDocumentoOrigemId: Guid.CreateVersion7(),
            tipoDocumentoCodigo: "LAUDO_MEDICO",
            tipoDocumentoNome: "Laudo médico",
            tipoDocumentoCategoria: "SAUDE",
            aplicabilidade: aplicabilidade,
            obrigatorio: obrigatorio,
            consequenciaIndeferimento: consequenciaIndeferimento,
            grupoSatisfacaoId: null);

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
}
