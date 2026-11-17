namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Invariantes de domínio do ato normativo append-only: normalização, shape do
/// hash do documento, positividade do ano, o par {id, hash} da versão do objeto e
/// a simetria de shape do par de retificação (ADR-0103). A fábrica é o alvo, então cada teste a
/// chama direto, com a essência à vista.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AtoNormativoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Registro = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly NumeracaoDoAto Numeracao = new("CEPS", "EDITAL", 2026, "13");
    private static readonly TipoDoAtoNoRegistro Tipo = new("EDITAL_ABERTURA", null, false, false, false);
    private static readonly EssenciaDocumentalDoAto Valida =
        new(Numeracao, Tipo, Publicacao, HashValido, PersonasDeTeste.NomeKevinCarvalho, default);

    [Fact(DisplayName = "Registrar tira os espaços das pontas dos textos")]
    public void Registrar_ComEspacosNasPontas_NormalizaOsTextos()
    {
        EssenciaDocumentalDoAto essencia = Valida with
        {
            Numeracao = new NumeracaoDoAto("  CEPS  ", "  EDITAL  ", 2026, "  13  "),
            Tipo = Tipo with { Codigo = "  EDITAL_ABERTURA  " },
            Assinante = $"  {PersonasDeTeste.NomeKevinCarvalho}  ",
        };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.Numeracao.Should().Be(Numeracao);
        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");
        ato.Assinante.Should().Be(PersonasDeTeste.NomeKevinCarvalho);
    }

    [Fact(DisplayName = "Registrar preenche os campos do ato sem retificação nem versão do objeto")]
    public void Registrar_ComCamposValidos_PreencheOsCampos()
    {
        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), Valida, Registro, versaoDoObjeto: null);

        ato.Id.Should().NotBe(Guid.Empty);
        ato.DataDoAto.Should().Be(Publicacao);
        ato.DocumentoHash.Should().Be(HashValido);
        ato.PublicadoEm.Should().Be(Registro);
        ato.VersaoDoObjeto.Should().BeNull();
        ato.AtoRetificadoId.Should().BeNull();
        ato.MotivoRetificacao.Should().BeNull();
    }

    [Theory(DisplayName = "Registrar copia cada atributo de consequência do tipo, nos dois valores")]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Registrar_CopiaOsAtributosDeConsequenciaDoTipo(bool congela, bool efeitoIrreversivel, bool unicoPorObjeto)
    {
        EssenciaDocumentalDoAto essencia = Valida with
        {
            Tipo = new TipoDoAtoNoRegistro("EDITAL_ABERTURA", null, congela, efeitoIrreversivel, unicoPorObjeto),
        };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.CongelaConfiguracao.Should().Be(congela);
        ato.EfeitoIrreversivel.Should().Be(efeitoIrreversivel);
        ato.UnicoPorObjeto.Should().Be(unicoPorObjeto);
    }

    [Fact(DisplayName = "Número em branco vira nulo (ato sem número é válido)")]
    public void Registrar_ComNumeroEmBranco_ArmazenaNulo()
    {
        EssenciaDocumentalDoAto essencia = Valida with { Numeracao = Numeracao with { Numero = "   " } };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "Guarda a versão do objeto por valor quando informada")]
    public void Registrar_ComVersaoDoObjeto_Guarda()
    {
        ReferenciaVersaoDoObjeto versao = ReferenciaVersaoDoObjeto
            .Criar(Guid.CreateVersion7(), HashValido).Value!;

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), Valida, Registro, versao);

        ato.VersaoDoObjeto.Should().Be(versao);
    }

    [Theory(DisplayName = "Hash do documento fora do formato SHA-256 é recusado")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")] // maiúsculo
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]  // 63 chars
    [InlineData("zzzz456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // não-hex
    public void Registrar_ComDocumentoHashInvalido_Lanca(string hash)
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { DocumentoHash = hash }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("documentoHash");
    }

    [Fact(DisplayName = "Hash do documento com quebra de linha no fim não é um SHA-256")]
    public void Registrar_HashDoDocumentoComQuebraDeLinhaFinal_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { DocumentoHash = HashValido + "\n" }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Órgão em branco é recusado")]
    public void Registrar_ComOrgaoEmBranco_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Numeracao = Numeracao with { Orgao = "" } }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("orgao");
    }

    [Fact(DisplayName = "Série em branco é recusada")]
    public void Registrar_ComSerieEmBranco_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Numeracao = Numeracao with { Serie = " " } }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("serie");
    }

    [Fact(DisplayName = "Código do tipo em branco é recusado")]
    public void Registrar_ComCodigoDoTipoEmBranco_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Tipo = Tipo with { Codigo = "" } }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("tipoCodigo");
    }

    [Fact(DisplayName = "Assinante em branco é recusado")]
    public void Registrar_ComAssinanteEmBranco_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Assinante = "  " }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("assinante");
    }

    [Theory(DisplayName = "Ano não-positivo é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_ComAnoNaoPositivo_Lanca(int ano)
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Numeracao = Numeracao with { Ano = ano } }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("ano");
    }

    [Fact(DisplayName = "Retificação preenche o par (ato retificado, motivo) e normaliza o motivo")]
    public void Registrar_ComRetificacao_PreencheParENormaliza()
    {
        Guid retificado = Guid.CreateVersion7();
        EssenciaDocumentalDoAto essencia = Valida with { Retificacao = new RelacaoDeRetificacao(retificado, "  corrige o anexo II  ") };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.AtoRetificadoId.Should().Be(retificado);
        ato.MotivoRetificacao.Should().Be("corrige o anexo II");
    }

    [Theory(DisplayName = "Par de retificação incompleto (só um dos dois) é recusado")]
    [InlineData(true, false)]   // só ato retificado
    [InlineData(false, true)]   // só motivo
    public void Registrar_ComRetificacaoIncompleta_Lanca(bool temRetificado, bool temMotivo)
    {
        Guid? retificado = temRetificado ? Guid.CreateVersion7() : null;
        string? motivo = temMotivo ? "corrige o anexo II" : null;
        EssenciaDocumentalDoAto essencia = Valida with { Retificacao = new RelacaoDeRetificacao(retificado, motivo) };

        Action acao = () => AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("atoRetificadoId");
    }

    [Fact(DisplayName = "Motivo em branco normaliza para ausente (par ausente é válido)")]
    public void Registrar_ComMotivoEmBranco_TrataComoAusente()
    {
        EssenciaDocumentalDoAto essencia = Valida with { Retificacao = new RelacaoDeRetificacao(null, "   ") };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.AtoRetificadoId.Should().BeNull();
        ato.MotivoRetificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Motivo acima do limite (1000) é recusado")]
    public void Registrar_ComMotivoLongoDemais_Lanca()
    {
        EssenciaDocumentalDoAto essencia = Valida with
        {
            Retificacao = new RelacaoDeRetificacao(Guid.CreateVersion7(), new string('x', 1001)),
        };

        Action acao = () => AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("motivoRetificacao");
    }

    [Fact(DisplayName = "Ato retificado com id vazio (Guid.Empty) é recusado")]
    public void Registrar_ComAtoRetificadoIdVazio_Lanca()
    {
        EssenciaDocumentalDoAto essencia = Valida with { Retificacao = new RelacaoDeRetificacao(Guid.Empty, "corrige o anexo II") };

        Action acao = () => AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("atoRetificadoId");
    }

    [Fact(DisplayName = "Nome do tipo é guardado por valor, sem espaços nas pontas")]
    public void Registrar_ComNomeDoTipo_GuardaNormalizado()
    {
        EssenciaDocumentalDoAto essencia = Valida with { Tipo = Tipo with { Nome = "  Edital de abertura  " } };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.TipoNome.Should().Be("Edital de abertura");
    }

    [Theory(DisplayName = "Nome do tipo ausente ou em branco fica nulo (ato sem nome, com o código presente)")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Registrar_SemNomeDoTipo_ArmazenaNulo(string? tipoNome)
    {
        EssenciaDocumentalDoAto essencia = Valida with { Tipo = Tipo with { Nome = tipoNome } };

        AtoNormativo ato = AtoNormativo.Registrar(Guid.CreateVersion7(), essencia, Registro, versaoDoObjeto: null);

        ato.TipoNome.Should().BeNull();
        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");
    }

    [Fact(DisplayName = "Nome do tipo acima do limite do catálogo (200) é recusado")]
    public void Registrar_ComNomeDoTipoLongoDemais_Lanca()
    {
        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), Valida with { Tipo = Tipo with { Nome = new string('x', 201) } }, Registro, versaoDoObjeto: null);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("tipoNome");
    }
}
