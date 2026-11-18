namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura da entidade forense <see cref="VersaoConfiguracao"/> (ADR-0104,
/// ADR-0063): numeração contígua a partir da abertura, contrato simétrico da
/// retificação (a versão 1 não retifica ninguém; toda versão seguinte
/// retifica) e derivação do hash a partir dos bytes canônicos (ADR-0100).
/// </summary>
/// <remarks>
/// A invariante de cadeia que depende do ESTADO do certame — a versão
/// corrente ser do processo certo — é regra de negócio da raiz, e aflora como
/// 422 em <see cref="ProcessoSeletivo.Retificar"/>; a cobertura dela vive em
/// <see cref="ProcessoSeletivoRetificarTests"/>. Aqui ficam os guards de última
/// linha da factory, que lançam: o agregado nunca materializa em estado
/// inválido, nem quando o caller erra.
/// </remarks>
public sealed class VersaoConfiguracaoTests
{
    private static readonly string HashAto = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly string OutroHashAto = string.Concat(Enumerable.Repeat("cd98765432", 7))[..64];

    private const string Algoritmo = "canonical-json/sha256@v1";

    private static byte[] BytesCanonicos(string status) =>
        Encoding.UTF8.GetBytes(new JsonObject { ["status"] = status }.ToJsonString());

    private static DocumentoCongelado Documento(string status, string? hash = null) =>
        new(BytesCanonicos(status), "1.0", Algoritmo, hash ?? HashAto);

    private static VersaoConfiguracao Abrir(Guid processoId, Guid atoCriadorId) =>
        VersaoConfiguracao.Abrir(processoId, Documento("abertura"), atoCriadorId, "user-conta-123", TimeProvider.System.GetUtcNow());

    private static VersaoConfiguracao Suceder(VersaoConfiguracao anterior, Guid atoCriadorId, DateTimeOffset instante) =>
        VersaoConfiguracao.Suceder(anterior, Documento("retificacao", OutroHashAto), atoCriadorId, "user-conta-123", instante);

    [Fact(DisplayName = "Abrir — a versão 1 abre a cadeia e não retifica ato algum")]
    public void Abrir_VersaoUm_NaoRetificaNinguem()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid atoCriadorId = Guid.CreateVersion7();

        VersaoConfiguracao versao = Abrir(processoId, atoCriadorId);

        versao.NumeroVersao.Should().Be(1, "a versão da abertura é a 1 (ADR-0104)");
        versao.AtoCriadorRetificaId.Should().BeNull("a abertura não emenda ato anterior — contrato simétrico");
        versao.ProcessoSeletivoId.Should().Be(processoId);
        versao.AtoCriadorId.Should().Be(atoCriadorId);
        versao.AtoCriadorHash.Should().Be(HashAto);
        versao.VigenteAPartirDe.Should().NotBe(default, "a vigência vem do relógio do sistema (ADR-0068)");
    }

    [Fact(DisplayName = "Abrir — o hash e o jsonb são DERIVADOS dos bytes canônicos, nunca recebidos")]
    public void Abrir_DerivaHashEJsonDosBytes()
    {
        byte[] bytes = BytesCanonicos("abertura");

        VersaoConfiguracao versao = Abrir(Guid.CreateVersion7(), Guid.CreateVersion7());

        versao.HashConfiguracao.Should().Be(
            HashCanonicalComputer.ComputeSha256Hex(bytes),
            "ADR-0100 itens 6/7: a evidência persistida não pode divergir dos bytes que a fundamentam");
        versao.ConfiguracaoCongelada.Should().Be(Encoding.UTF8.GetString(bytes));
    }

    [Fact(DisplayName = "Abrir — mutar o array do caller depois não alcança os bytes congelados")]
    public void Abrir_CopiaDefensivaDosBytes()
    {
        byte[] bytesDoCaller = BytesCanonicos("abertura");
        DocumentoCongelado documento = new(bytesDoCaller, "1.0", Algoritmo, HashAto);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(Guid.CreateVersion7(), documento, Guid.CreateVersion7(), "user-conta-123", TimeProvider.System.GetUtcNow());

        bytesDoCaller[0] = (byte)'X';

        HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica)
            .Should().Be(versao.HashConfiguracao,
                "os bytes persistidos têm de continuar provando o hash — mutar o array do caller não pode alcançá-los");
    }

    [Fact(DisplayName = "O número da versão da abertura é o 1, e o da sucessora é o da anterior mais um")]
    public void Numeros_AberturaESucessora()
    {
        VersaoConfiguracao.NumeroDaAbertura.Should().Be(1);
        VersaoConfiguracao.NumeroDaSucessora(VersaoConfiguracao.NumeroDaAbertura).Should().Be(2);
        VersaoConfiguracao.NumeroDaSucessora(7).Should().Be(8);
    }

    [Fact(DisplayName = "Suceder — a versão N + 1 retifica o ato criador da versão N, e a numeração é contígua por construção")]
    public void Suceder_NumeracaoContiguaEElo()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid atoAbertura = Guid.CreateVersion7();
        Guid atoRetificador = Guid.CreateVersion7();
        VersaoConfiguracao versao1 = Abrir(processoId, atoAbertura);

        VersaoConfiguracao versao2 = Suceder(versao1, atoRetificador, TimeProvider.System.GetUtcNow());

        versao2.NumeroVersao.Should().Be(2, "o número é derivado da versão anterior — buraco é impossível por construção");
        versao2.ProcessoSeletivoId.Should().Be(processoId, "a sucessora herda o certame da versão que sucede");
        versao2.AtoCriadorId.Should().Be(atoRetificador);
        versao2.AtoCriadorHash.Should().Be(OutroHashAto);
    }

    [Fact(DisplayName = "Suceder — o ato retificado vem do ato criador da versão anterior, e nunca de quem chama")]
    public void Suceder_AtoRetificadoEOCriadorDaAnterior()
    {
        VersaoConfiguracao versao1 = Abrir(Guid.CreateVersion7(), Guid.CreateVersion7());

        VersaoConfiguracao versao2 = Suceder(versao1, Guid.CreateVersion7(), TimeProvider.System.GetUtcNow());

        versao2.AtoCriadorRetificaId.Should().Be(versao1.AtoCriadorId);
    }

    [Fact(DisplayName = "Suceder — relógio que anda para trás não faz a vigência regredir: a sucessora empata com a anterior")]
    public void Suceder_RelogioRetrocede_VigenciaNaoRegride()
    {
        DateTimeOffset abertura = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
        VersaoConfiguracao versao1 = VersaoConfiguracao.Abrir(Guid.CreateVersion7(), Documento("abertura"), Guid.CreateVersion7(), "user-conta-123", abertura);

        // Ajuste NTP em degrau: o relógio do host recua entre a abertura e a retificação. Como é a VIGÊNCIA que
        // ordena as versões (ADR-0104), deixar a sucessora nascer no passado faria o seletor continuar elegendo a
        // versão velha depois de a nova existir.
        VersaoConfiguracao versao2 = Suceder(versao1, Guid.CreateVersion7(), abertura.AddMinutes(-10));

        versao2.VigenteAPartirDe.Should().Be(
            versao1.VigenteAPartirDe,
            "a sucessora empata no instante da anterior — o empate é permitido, e o desempate por número elege a mais nova");
        versao2.NumeroVersao.Should().Be(2);
    }

    [Fact(DisplayName = "A vigência da sucessora é o instante pedido, e nunca precede a da versão")]
    public void VigenciaDaSucessora_NuncaPrecedeAVersao()
    {
        DateTimeOffset abertura = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(Guid.CreateVersion7(), Documento("abertura"), Guid.CreateVersion7(), "user-conta-123", abertura);

        versao.VigenciaDaSucessora(abertura.AddMinutes(-1)).Should().Be(abertura);
        versao.VigenciaDaSucessora(abertura).Should().Be(abertura);
        versao.VigenciaDaSucessora(abertura.AddMinutes(1)).Should().Be(abertura.AddMinutes(1));
    }

    [Fact(DisplayName = "Suceder — o ato criador da versão anterior não pode criar a seguinte (um ato congela uma vez)")]
    public void Suceder_MesmoAtoCriador_Lanca()
    {
        Guid atoAbertura = Guid.CreateVersion7();
        VersaoConfiguracao versao1 = Abrir(Guid.CreateVersion7(), atoAbertura);

        Action sucederComOMesmoAto = () => Suceder(versao1, atoAbertura, TimeProvider.System.GetUtcNow());

        sucederComOMesmoAto.Should().Throw<ArgumentException>()
            .WithParameterName("atoCriadorId");
    }

    [Fact(DisplayName = "Abrir — versão sem ato criador é recusada")]
    public void Abrir_SemAtoCriador_Lanca()
    {
        Action abrirSemAto = () => Abrir(Guid.CreateVersion7(), Guid.Empty);

        abrirSemAto.Should().Throw<ArgumentException>()
            .WithParameterName("atoCriadorId");
    }

    [Fact(DisplayName = "Abrir — hash do ato criador fora do formato SHA-256 hex é recusado")]
    public void Abrir_HashDoAtoMalformado_Lanca()
    {
        Action abrirComHashInvalido = () => VersaoConfiguracao.Abrir(
            Guid.CreateVersion7(), Documento("abertura", "NAO-E-UM-HASH"), Guid.CreateVersion7(), "user-conta-123", TimeProvider.System.GetUtcNow());

        abrirComHashInvalido.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Abrir — configuração congelada vazia é recusada")]
    public void Abrir_ConfiguracaoVazia_Lanca()
    {
        Action abrirSemBytes = () => VersaoConfiguracao.Abrir(
            Guid.CreateVersion7(), new DocumentoCongelado([], "1.0", Algoritmo, HashAto), Guid.CreateVersion7(), "user-conta-123", TimeProvider.System.GetUtcNow());

        abrirSemBytes.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Abrir — versão do conjunto de blocos ou algoritmo de hash em branco é recusado")]
    [InlineData("", Algoritmo)]
    [InlineData(" ", Algoritmo)]
    [InlineData("1.0", "")]
    [InlineData("1.0", " ")]
    public void Abrir_SchemaOuAlgoritmoEmBranco_Lanca(string schemaVersion, string algoritmoHash)
    {
        DocumentoCongelado documento = new(BytesCanonicos("abertura"), schemaVersion, algoritmoHash, HashAto);

        Action abrirComDocumentoIncompleto = () => VersaoConfiguracao.Abrir(Guid.CreateVersion7(), documento, Guid.CreateVersion7(), "user-conta-123", TimeProvider.System.GetUtcNow());

        abrirComDocumentoIncompleto.Should().Throw<ArgumentException>();
    }
}
