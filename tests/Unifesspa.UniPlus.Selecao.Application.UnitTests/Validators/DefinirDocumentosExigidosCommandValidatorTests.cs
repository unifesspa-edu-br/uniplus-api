namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Validators;

using System.Text.Json;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="DefinirDocumentosExigidosCommandValidator"/> para a base legal
/// (Story #554, PR #898, issue #549) — valida apenas a FORMA de cada item; o gate "≥1
/// RESOLVIDO por exigência que determina resultado" é do domínio, na publicação.
/// </summary>
/// <remarks>
/// FormatosPermitidos (Story #918) não tem cobertura aqui — a interpretação do valor JSON
/// polimórfico é do HANDLER (<c>DefinirDocumentosExigidosCommandHandlerTests</c>), e a
/// coerência de negócio é do domínio (<c>FormatosPermitidosTests</c>); esta suíte só cobre a
/// FORMA validada por FluentValidation, que não inclui mais este campo.
/// </remarks>
public sealed class DefinirDocumentosExigidosCommandValidatorTests
{
    private static readonly JsonElement Qualquer = JsonSerializer.SerializeToElement("QUALQUER");

    private static ItemDocumentoExigidoDeTeste ItemGeral() =>
        ItemDocumentoExigidoDeTeste.Geral(Guid.CreateVersion7(), Guid.CreateVersion7());

    private static ItemDocumentoExigidoInput ItemCom(params BaseLegalInput[] basesLegais) =>
        ItemGeral().ComBasesLegais(basesLegais).Construir();

    private static ItemDocumentoExigidoInput ItemComIdade(
        IdadeMaximaEmissaoInput? idadeMaximaEmissao = null, int? tamanhoMaximoBytes = null) =>
        ItemGeral().ComIdadeMaxima(idadeMaximaEmissao).ComTamanhoMaximo(tamanhoMaximoBytes).Construir();

    private static ValidationResult Validar(ItemDocumentoExigidoInput item) =>
        ValidarRegra(RegraDeComprovacaoDeTeste.DeDocumento(item).Construir());

    [Fact(DisplayName = "Item sem base legal (lista vazia) é aceito — a coerência com o gate é da publicação")]
    public void Aceita_SemBaseLegal() =>
        Validar(ItemCom()).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Base legal com dados coerentes é aceita")]
    public void Aceita_BaseLegalValida() =>
        Validar(ItemCom(new BaseLegalInput("Lei 12.711/2012, art. 3º", "FEDERAL", "RESOLVIDO", null))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Validator passa com referência vazia — a rejeição é do agregado (DocumentoExigidoBaseLegal.Criar, ADR-0125)")]
    public void Aceita_ReferenciaBaseLegalVaziaNoValidator()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("", "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Validator passa com referência só de espaços — a rejeição é do agregado (DocumentoExigidoBaseLegal.Criar, ADR-0125)")]
    public void Aceita_ReferenciaBaseLegalSoDeEspacosNoValidator()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("   ", "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Validator passa com abrangência fora do domínio — a rejeição é do agregado (DocumentoExigidoBaseLegal.Criar, ADR-0125)")]
    public void Aceita_AbrangenciaDesconhecidaNoValidator()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Lei X", "PLANETARIA", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Fact(DisplayName = "Validator passa com status fora do domínio — a rejeição é do agregado (DocumentoExigidoBaseLegal.Criar, ADR-0125)")]
    public void Aceita_StatusDesconhecidoNoValidator()
    {
        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Lei X", "FEDERAL", "EM_ANALISE", null)));

        resultado.IsValid.Should().BeTrue(string.Join("; ", resultado.Errors));
    }

    [Theory(DisplayName = "Todas as abrangências e status válidos são aceitos")]
    [InlineData("FEDERAL", "PENDENTE")]
    [InlineData("ESTADUAL", "RESOLVIDO")]
    [InlineData("MUNICIPAL", "PENDENTE")]
    [InlineData("INTERNA_NORMA", "RESOLVIDO")]
    [InlineData("INTERNA_EDITAL", "RESOLVIDO")]
    public void Aceita_TodasCombinacoesValidas(string abrangencia, string status) =>
        Validar(ItemCom(new BaseLegalInput("Referência", abrangencia, status, "Observação"))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Referência acima de 500 caracteres é rejeitada — o mesmo teto de DocumentoExigidoBaseLegalConfiguration")]
    public void Rejeita_ReferenciaAcimaDoTetoDePersistencia()
    {
        string referenciaLonga = new('a', 501);

        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput(referenciaLonga, "FEDERAL", "RESOLVIDO", null)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("500 caracteres", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Referência com exatamente 500 caracteres é aceita (contraprova do limite)")]
    public void Aceita_ReferenciaNoTetoDePersistencia() =>
        Validar(ItemCom(new BaseLegalInput(new string('a', 500), "FEDERAL", "RESOLVIDO", null))).IsValid.Should().BeTrue();

    [Fact(DisplayName = "Observação acima de 1000 caracteres é rejeitada")]
    public void Rejeita_ObservacaoAcimaDoTetoDePersistencia()
    {
        string observacaoLonga = new('a', 1001);

        ValidationResult resultado = Validar(ItemCom(new BaseLegalInput("Referência", "FEDERAL", "RESOLVIDO", observacaoLonga)));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("1000 caracteres", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Item de base legal nulo na lista é rejeitado")]
    public void Rejeita_ItemDeBaseLegalNulo()
    {
        ItemDocumentoExigidoInput item = ItemGeral().ComBasesLegais([null!]).ComFormatos(null).Construir();

        ValidationResult resultado = Validar(item);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Item de base legal", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Múltiplas bases legais no mesmo item são aceitas (1:N)")]
    public void Aceita_MultiplasBasesLegais() =>
        Validar(ItemCom(
            new BaseLegalInput("Lei Federal X", "FEDERAL", "RESOLVIDO", null),
            new BaseLegalInput("Cláusula do edital", "INTERNA_EDITAL", "PENDENTE", null)))
            .IsValid.Should().BeTrue();

    // ── Story #554/issue #893 (PR #900) — idade máxima de emissão e tamanho ──

    [Fact(DisplayName = "Item sem idade/tamanho (tudo nulo, exceto FormatosPermitidos=QUALQUER) é aceito")]
    public void Aceita_SemIdadeTamanho() =>
        Validar(ItemComIdade()).IsValid.Should().BeTrue();

    [Fact(DisplayName = "IdadeMaximaEmissao com Unidade fora do domínio é rejeitada")]
    public void Rejeita_UnidadeDesconhecida()
    {
        IdadeMaximaEmissaoInput idade = new(90, "SEMANAS", "DATA_SUBMISSAO", null, null);

        ValidationResult resultado = Validar(ItemComIdade(idade));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("Unidade", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "IdadeMaximaEmissao com ReferenciaTipo fora do domínio é rejeitada")]
    public void Rejeita_ReferenciaTipoDesconhecido()
    {
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "DATA_DE_HOJE", null, null);

        ValidationResult resultado = Validar(ItemComIdade(idade));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.ErrorMessage.Contains("ReferenciaTipo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "IdadeMaximaEmissao com Valor zero ou negativo é rejeitada")]
    public void Rejeita_ValorNaoPositivo()
    {
        IdadeMaximaEmissaoInput idade = new(0, "DIAS", "DATA_SUBMISSAO", null, null);

        Validar(ItemComIdade(idade)).IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Todas as unidades e tipos de referência válidos são aceitos (forma)")]
    [InlineData("DIAS", "FIM_INSCRICAO")]
    [InlineData("MESES", "DATA_SUBMISSAO")]
    [InlineData("ANOS", "DATA_SUBMISSAO")]
    public void Aceita_TodasCombinacoesValidasDeIdade(string unidade, string referenciaTipo)
    {
        IdadeMaximaEmissaoInput idade = new(90, unidade, referenciaTipo, null, null);

        Validar(ItemComIdade(idade)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Validator passa com TamanhoMaximoBytes zero ou negativo — a rejeição é do agregado (DocumentoExigido.Criar, ADR-0125)")]
    public void Aceita_TamanhoMaximoBytesNaoPositivoNoValidator()
    {
        Validar(ItemComIdade(tamanhoMaximoBytes: 0)).IsValid.Should().BeTrue();
        Validar(ItemComIdade(tamanhoMaximoBytes: -1)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "TamanhoMaximoBytes positivo é aceito")]
    public void Aceita_TamanhoMaximoBytesPositivo() =>
        Validar(ItemComIdade(tamanhoMaximoBytes: 5_000_000)).IsValid.Should().BeTrue();

    // ── Cardinalidade qualificada — RegraDeComprovacaoInformadaValidator, chave da distinção ──

    private static ValidationResult ValidarRegra(RegraDeComprovacaoInformada regraInformada) =>
        new DefinirDocumentosExigidosCommandValidator().Validate(
            new DefinirDocumentosExigidosCommand(Guid.CreateVersion7(), [regraInformada]));

    [Theory(DisplayName = "Critério de distinção desconhecido segue para a conferência do domínio")]
    [InlineData("")]
    [InlineData("   ")]
    public void AceitaForma_CriterioDeDistincaoDasApresentacoesVaziaOuEmBranco(string chaveInvalida)
    {
        RegraDeComprovacaoInformada regraComDistincao = RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).ComDistincao(chaveInvalida).Construir();

        ValidationResult resultado = ValidarRegra(regraComDistincao);

        resultado.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "A chave de distinção do catálogo fechado é aceita")]
    public void Aceita_CriterioDeDistincaoDasApresentacoesValida()
    {
        RegraDeComprovacaoInformada regraComDistincao = RegraDeComprovacaoDeTeste.DeDocumento(ItemCom())
            .ComQuantidadeMinima(1)
            .ComDistincao("COMPETENCIA_MENSAL", new DateOnly(2026, 1, 1))
            .Construir();

        ValidarRegra(regraComDistincao).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Quantidade mínima do grupo é conferida pelo domínio")]
    public void Aceita_QuantidadeMinimaEmGrupoDeTodosOsItens_ParaConferenciaPeloDominio()
    {
        RegraDeComprovacaoInformada grupoDeTodosOsItens = RegraDeComprovacaoDeTeste.Grupo("TODOS_OS_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).Construir()).ComQuantidadeMinima(2).Construir();

        ValidarRegra(grupoDeTodosOsItens).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Grupo com quantidade mínima de itens não admite distinção de apresentações")]
    public void Rejeita_DistincaoEmGrupoDeQuantidadeMinima()
    {
        RegraDeComprovacaoInformada grupoDeQuantidadeMinima = RegraDeComprovacaoDeTeste.Grupo("QUANTIDADE_MINIMA_DE_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).Construir()).ComDistincao("OCORRENCIA").Construir();

        ValidarRegra(grupoDeQuantidadeMinima).Errors.Should().Contain(e => e.ErrorMessage.Contains("critério de distinção", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Regra de documento não admite consequência própria de grupo")]
    public void Rejeita_IndeferimentoDeGrupoEmRegraDeDocumento()
    {
        RegraDeComprovacaoInformada regraDeDocumento = RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).ComIndeferimento("ELIMINA").Construir();

        ValidarRegra(regraDeDocumento).Errors.Should().Contain(e => e.ErrorMessage.Contains("consequência própria", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Base legal sem consequência é conferida pelo domínio")]
    public void Rejeita_BasesLegaisDeGrupoSemConsequencia()
    {
        RegraDeComprovacaoInformada grupoDeQuantidadeMinima = RegraDeComprovacaoDeTeste.Grupo("QUANTIDADE_MINIMA_DE_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).Construir())
            .ComIndeferimento(null, new BaseLegalInput("Lei X", "FEDERAL", "RESOLVIDO", null))
            .Construir();

        ValidarRegra(grupoDeQuantidadeMinima).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Grupo com quantidade mínima de itens aceita consequência e bases legais")]
    public void Aceita_GrupoDeQuantidadeMinimaComConsequenciaEBasesLegais()
    {
        RegraDeComprovacaoInformada grupoDeQuantidadeMinima = RegraDeComprovacaoDeTeste.Grupo("QUANTIDADE_MINIMA_DE_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(ItemCom()).Construir())
            .ComQuantidadeMinima(1)
            .ComIndeferimento("ELIMINA", new BaseLegalInput("Lei X", "FEDERAL", "RESOLVIDO", null))
            .Construir();

        ValidarRegra(grupoDeQuantidadeMinima).IsValid.Should().BeTrue();
    }
}
