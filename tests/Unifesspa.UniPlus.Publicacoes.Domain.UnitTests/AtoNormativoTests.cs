namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Invariantes de domínio do ato normativo append-only: normalização, shape do
/// hash do documento, positividade do ano, o par {id, hash} da versão invocada e
/// a simetria de shape do par de retificação (ADR-0103).
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

    [Fact(DisplayName = "Registrar normaliza os textos e preenche os campos")]
    public void Registrar_ComCamposValidos_NormalizaEPreenche()
    {
        AtoNormativo ato = AtoNormativo.Registrar(
            orgao: "  CEPS  ",
            serie: "  EDITAL  ",
            ano: 2026,
            numero: "  13  ",
            tipoCodigo: "  EDITAL_ABERTURA  ",
            congelaConfiguracao: true,
            efeitoIrreversivel: false,
            unicoPorObjeto: true,
            dataPublicacao: Publicacao,
            documentoHash: HashValido,
            assinante: "  Jairo Belchior  ",
            registradoEm: Registro,
            versaoInvocada: null);

        ato.Id.Should().NotBe(Guid.Empty);
        ato.Orgao.Should().Be("CEPS");
        ato.Serie.Should().Be("EDITAL");
        ato.Ano.Should().Be(2026);
        ato.Numero.Should().Be("13");
        ato.TipoCodigo.Should().Be("EDITAL_ABERTURA");
        ato.CongelaConfiguracao.Should().BeTrue();
        ato.EfeitoIrreversivel.Should().BeFalse();
        ato.UnicoPorObjeto.Should().BeTrue();
        ato.DataPublicacao.Should().Be(Publicacao);
        ato.DocumentoHash.Should().Be(HashValido);
        ato.Assinante.Should().Be("Jairo Belchior");
        ato.RegistradoEm.Should().Be(Registro);
        ato.VersaoInvocada.Should().BeNull();
        ato.AtoRetificadoId.Should().BeNull();
        ato.MotivoRetificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Número em branco vira nulo (ato sem número é válido)")]
    public void Registrar_ComNumeroEmBranco_ArmazenaNulo()
    {
        AtoNormativo ato = Registrar(numero: "   ");
        ato.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "Guarda a versão invocada por valor quando informada")]
    public void Registrar_ComVersaoInvocada_Guarda()
    {
        ReferenciaVersaoConfiguracao versao = ReferenciaVersaoConfiguracao
            .Criar(Guid.CreateVersion7(), HashValido).Value!;

        AtoNormativo ato = Registrar(versao: versao);

        ato.VersaoInvocada.Should().Be(versao);
    }

    [Theory(DisplayName = "Hash do documento fora do formato SHA-256 é recusado")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")] // maiúsculo
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]  // 63 chars
    [InlineData("zzzz456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // não-hex
    public void Registrar_ComDocumentoHashInvalido_Lanca(string hash)
    {
        Action acao = () => Registrar(documentoHash: hash);
        acao.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Órgão, série, tipo e assinante em branco são recusados")]
    [InlineData("", "EDITAL", "EDITAL_ABERTURA", "Assinante")]
    [InlineData("CEPS", " ", "EDITAL_ABERTURA", "Assinante")]
    [InlineData("CEPS", "EDITAL", "", "Assinante")]
    [InlineData("CEPS", "EDITAL", "EDITAL_ABERTURA", "  ")]
    public void Registrar_ComTextoObrigatorioEmBranco_Lanca(
        string orgao, string serie, string tipoCodigo, string assinante)
    {
        Action acao = () => Registrar(orgao: orgao, serie: serie, tipoCodigo: tipoCodigo, assinante: assinante);
        acao.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Ano não-positivo é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_ComAnoNaoPositivo_Lanca(int ano)
    {
        Action acao = () => Registrar(ano: ano);
        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Registrar copia unico_por_objeto por valor do catálogo")]
    public void Registrar_CopiaUnicoPorObjeto()
    {
        AtoNormativo ato = Registrar(unicoPorObjeto: true);
        ato.UnicoPorObjeto.Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação preenche o par (ato retificado, motivo) e normaliza o motivo")]
    public void Registrar_ComRetificacao_PreencheParENormaliza()
    {
        Guid retificado = Guid.CreateVersion7();
        AtoNormativo ato = Registrar(atoRetificadoId: retificado, motivoRetificacao: "  corrige o anexo II  ");

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

        Action acao = () => Registrar(atoRetificadoId: retificado, motivoRetificacao: motivo);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Motivo em branco normaliza para ausente (par ausente é válido)")]
    public void Registrar_ComMotivoEmBranco_TrataComoAusente()
    {
        AtoNormativo ato = Registrar(atoRetificadoId: null, motivoRetificacao: "   ");
        ato.AtoRetificadoId.Should().BeNull();
        ato.MotivoRetificacao.Should().BeNull();
    }

    [Fact(DisplayName = "Motivo acima do limite (1000) é recusado")]
    public void Registrar_ComMotivoLongoDemais_Lanca()
    {
        Action acao = () => Registrar(
            atoRetificadoId: Guid.CreateVersion7(),
            motivoRetificacao: new string('x', 1001));
        acao.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Ato retificado com id vazio (Guid.Empty) é recusado")]
    public void Registrar_ComAtoRetificadoIdVazio_Lanca()
    {
        Action acao = () => Registrar(atoRetificadoId: Guid.Empty, motivoRetificacao: "corrige o anexo II");
        acao.Should().Throw<ArgumentException>();
    }

    private static AtoNormativo Registrar(
        string orgao = "CEPS",
        string serie = "EDITAL",
        int ano = 2026,
        string? numero = "13",
        string tipoCodigo = "EDITAL_ABERTURA",
        bool unicoPorObjeto = false,
        string documentoHash = HashValido,
        string assinante = "Jairo Belchior",
        ReferenciaVersaoConfiguracao? versao = null,
        Guid? atoRetificadoId = null,
        string? motivoRetificacao = null) =>
        AtoNormativo.Registrar(
            orgao, serie, ano, numero, tipoCodigo,
            congelaConfiguracao: false,
            efeitoIrreversivel: false,
            unicoPorObjeto: unicoPorObjeto,
            dataPublicacao: Publicacao,
            documentoHash: documentoHash,
            assinante: assinante,
            registradoEm: Registro,
            versaoInvocada: versao,
            atoRetificadoId: atoRetificadoId,
            motivoRetificacao: motivoRetificacao);
}
