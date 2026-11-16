namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.FatoCandidatoDeTeste;

/// <summary>
/// O ponto de resolução e o vínculo (binding) do fato: formato, prefixo coerente com a origem e a
/// referência da regra de derivação.
/// </summary>
public sealed class FatoCandidatoVinculoTests
{
    [Theory(DisplayName = "Fato do administrador não usa vínculo que exige código do sistema")]
    [InlineData(OrigemFato.Derivado, "ATRIBUTO_CANDIDATO:DADO")]
    [InlineData(OrigemFato.Derivado, "CLASSIFICACAO:DADO")]
    [InlineData(OrigemFato.Integracao, "INTEGRACAO:DADO")]
    public void Criar_FatoDoAdministradorComVinculoDeSistema_Recusa(OrigemFato origem, string binding) =>
        Criar(codigo: "DADO", origem: origem, binding: binding)
            .Error!.Code.Should().Be(FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema);

    [Theory(DisplayName = "Ponto de resolução ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PontoResolucaoAusente_Falha(string ponto)
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: ponto);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoObrigatorio);
    }

    [Fact(DisplayName = "Ponto de resolução fora do conjunto canônico de fases é rejeitado")]
    public void Criar_PontoResolucaoForaDoCanonico_Falha()
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: "FASE_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoInvalido);
    }

    // ─── Binding (ADR-0116) ─────────────────────────────────────────────────

    [Theory(DisplayName = "Binding ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_BindingAusente_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingObrigatorio);
    }

    [Theory(DisplayName = "Binding sem separador, ou com prefixo/referência vazios, é rejeitado como formato inválido")]
    [InlineData("CAMPO_FORMULARIO_COR_RACA")]
    [InlineData(":COR_RACA")]
    [InlineData("CAMPO_FORMULARIO:")]
    public void Criar_BindingFormatoInvalido_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingFormatoInvalido);
    }

    [Theory(DisplayName = "Binding com prefixo incoerente com a origem é rejeitado")]
    [InlineData(OrigemFato.Declarado, "ATRIBUTO_CANDIDATO:COR_RACA")]
    [InlineData(OrigemFato.Declarado, "REGRA_DERIVACAO:MODALIDADE")]
    [InlineData(OrigemFato.Derivado, "CAMPO_FORMULARIO:FAIXA_ETARIA")]
    [InlineData(OrigemFato.Integracao, "CAMPO_FORMULARIO:ANO_ENEM")]
    [InlineData(OrigemFato.Integracao, "REGRA_DERIVACAO:ALGO")]
    public void Criar_BindingPrefixoIncoerenteComOrigem_Falha(OrigemFato origem, string binding)
    {
        Result<FatoCandidato> resultado = Criar(origem: origem, binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência não é o código do próprio fato é recusado")]
    public void Criar_BindingRegraDerivacaoReferenciaOutroFato_Falha()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "FAIXA_ETARIA", origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsFailure.Should().BeTrue(
            "a regra de derivação referenciada é a do próprio fato — apontar para outro congelaria metadado de derivação alheio");
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência é o código do próprio fato é aceito")]
    public void Criar_BindingRegraDerivacaoReferenciaProprioFato_Aceita()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "MODALIDADE", dominio: DominioFato.Categorico,
            cardinalidade: CardinalidadeFato.Multivalorado,
            origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mensagem de prefixo incoerente lista os dois prefixos aceitos quando a origem é derivada")]
    public void Criar_BindingPrefixoIncoerente_Derivado_MensagemListaOsDois()
    {
        Result<FatoCandidato> resultado = Criar(origem: OrigemFato.Derivado, binding: "CAMPO_FORMULARIO:X");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("ATRIBUTO_CANDIDATO")
            .And.Contain("REGRA_DERIVACAO",
                "a origem derivada aceita dois prefixos — a mensagem precisa apontar ambos, não um só");
    }

    [Theory(DisplayName = "Binding com prefixo coerente com a origem é aceito")]
    [InlineData(OrigemFato.Declarado, "CAMPO_FORMULARIO:COR_RACA")]
    [InlineData(OrigemFato.Derivado, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA")]
    // REGRA_DERIVACAO referencia o próprio fato: a referência bate com o código FATO_QUALQUER.
    [InlineData(OrigemFato.Derivado, "REGRA_DERIVACAO:FATO_QUALQUER")]
    [InlineData(OrigemFato.Integracao, "INTEGRACAO:ANO_ENEM")]
    public void Criar_BindingPrefixoCoerenteComOrigem_Aceita(OrigemFato origem, string binding)
    {
        // Atributo do candidato e integração só existem em fato de sistema.
        Result<FatoCandidato> resultado = Criar(
            codigo: "FATO_QUALQUER", dominio: DominioFato.Numerico,
            origem: origem, binding: binding, sistema: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Binding.Should().Be(binding);
    }

    // ─── AdicionarValorDominio (ADR-0116) ───────────────────────────────────
}
