namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura da entidade forense <see cref="VersaoConfiguracao"/> (ADR-0104,
/// ADR-0063): numeração contígua a partir da abertura, contrato simétrico da
/// retificação (a versão 1 não retifica ninguém; toda versão seguinte
/// retifica) e derivação do hash a partir dos bytes canônicos (ADR-0100).
/// </summary>
/// <remarks>
/// As invariantes de cadeia que dependem do ESTADO do certame — a versão
/// corrente ser do processo certo, e o ato criador emendar o ato criador dela —
/// são regra de negócio da raiz, e afloram como 422 em
/// <see cref="ProcessoSeletivo.Retificar"/>; a cobertura delas vive em
/// <see cref="ProcessoSeletivoRetificarTests"/>. Aqui ficam os guards de última
/// linha da factory, que lançam: o agregado nunca materializa em estado
/// inválido, nem quando o caller erra.
/// </remarks>
public sealed class VersaoConfiguracaoTests
{
    private static readonly string HashAto = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly string OutroHashAto = string.Concat(Enumerable.Repeat("cd98765432", 7))[..64];

    private static byte[] BytesCanonicos(string status) =>
        Encoding.UTF8.GetBytes(new JsonObject { ["status"] = status }.ToJsonString());

    private static VersaoConfiguracao Abrir(Guid processoId, Guid atoCriadorId) =>
        VersaoConfiguracao.Abrir(
            processoId,
            BytesCanonicos("abertura"),
            schemaVersion: "1.0",
            algoritmoHash: "canonical-json/sha256@v1",
            atoCriadorId,
            atoCriadorHash: HashAto,
            atorUsuarioSub: "user-sub-123",
            TimeProvider.System);

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
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            Guid.CreateVersion7(),
            bytesDoCaller,
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            HashAto,
            "user-sub-123",
            TimeProvider.System);

        bytesDoCaller[0] = (byte)'X';

        HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica)
            .Should().Be(versao.HashConfiguracao,
                "os bytes persistidos têm de continuar provando o hash — mutar o array do caller não pode alcançá-los");
    }

    [Fact(DisplayName = "Suceder — a versão N + 1 retifica o ato criador da versão N, e a numeração é contígua por construção")]
    public void Suceder_NumeracaoContiguaEElo()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid atoAbertura = Guid.CreateVersion7();
        Guid atoRetificador = Guid.CreateVersion7();
        VersaoConfiguracao versao1 = Abrir(processoId, atoAbertura);

        VersaoConfiguracao versao2 = VersaoConfiguracao.Suceder(
            versao1,
            BytesCanonicos("retificacao"),
            "1.0",
            "canonical-json/sha256@v1",
            atoRetificador,
            OutroHashAto,
            atoCriadorRetificaId: atoAbertura,
            "user-sub-123",
            TimeProvider.System);

        versao2.NumeroVersao.Should().Be(2, "o número é derivado da versão anterior — buraco é impossível por construção");
        versao2.ProcessoSeletivoId.Should().Be(processoId, "a sucessora herda o certame da versão que sucede");
        versao2.AtoCriadorId.Should().Be(atoRetificador);
        versao2.AtoCriadorRetificaId.Should().Be(atoAbertura, "o ato criador da versão 2 retifica o ato criador da versão 1");
    }

    [Fact(DisplayName = "Suceder — relógio que anda para trás não faz a vigência regredir: a sucessora empata com a anterior")]
    public void Suceder_RelogioRetrocede_VigenciaNaoRegride()
    {
        Guid atoAbertura = Guid.CreateVersion7();
        RelogioManual clock = new(new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero));

        VersaoConfiguracao versao1 = VersaoConfiguracao.Abrir(
            Guid.CreateVersion7(),
            BytesCanonicos("abertura"),
            "1.0",
            "canonical-json/sha256@v1",
            atoAbertura,
            HashAto,
            "user-sub-123",
            clock);

        // Ajuste NTP em degrau: o relógio do host recua entre a abertura e a
        // retificação. Como é a VIGÊNCIA que ordena as versões (ADR-0104), deixar
        // a sucessora nascer no passado faria o seletor continuar elegendo a
        // versão velha depois de a nova existir.
        clock.Avancar(TimeSpan.FromMinutes(-10));

        VersaoConfiguracao versao2 = VersaoConfiguracao.Suceder(
            versao1,
            BytesCanonicos("retificacao"),
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            OutroHashAto,
            atoCriadorRetificaId: atoAbertura,
            "user-sub-123",
            clock);

        versao2.VigenteAPartirDe.Should().Be(
            versao1.VigenteAPartirDe,
            "a sucessora empata no instante da anterior — o empate é permitido, e o desempate por número elege a mais nova");
        versao2.NumeroVersao.Should().Be(2);
    }

    [Fact(DisplayName = "Suceder — ato criador que não emenda o criador da versão anterior é erro de programação (a raiz já barrou como 422)")]
    public void Suceder_AtoCriadorNaoRetificaAnterior_Lanca()
    {
        VersaoConfiguracao versao1 = Abrir(Guid.CreateVersion7(), Guid.CreateVersion7());

        Action sucederForaDaCadeia = () => VersaoConfiguracao.Suceder(
            versao1,
            BytesCanonicos("retificacao"),
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            OutroHashAto,
            atoCriadorRetificaId: Guid.CreateVersion7(),
            "user-sub-123",
            TimeProvider.System);

        sucederForaDaCadeia.Should().Throw<ArgumentException>()
            .WithParameterName("atoCriadorRetificaId");
    }

    [Fact(DisplayName = "Suceder — o ato criador da versão anterior não pode criar a seguinte (um ato congela uma vez)")]
    public void Suceder_MesmoAtoCriador_Lanca()
    {
        Guid atoAbertura = Guid.CreateVersion7();
        VersaoConfiguracao versao1 = Abrir(Guid.CreateVersion7(), atoAbertura);

        Action sucederComOMesmoAto = () => VersaoConfiguracao.Suceder(
            versao1,
            BytesCanonicos("retificacao"),
            "1.0",
            "canonical-json/sha256@v1",
            atoAbertura,
            HashAto,
            atoCriadorRetificaId: atoAbertura,
            "user-sub-123",
            TimeProvider.System);

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
            Guid.CreateVersion7(),
            BytesCanonicos("abertura"),
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            "NAO-E-UM-HASH",
            "user-sub-123",
            TimeProvider.System);

        abrirComHashInvalido.Should().Throw<ArgumentException>()
            .WithParameterName("atoCriadorHash");
    }

    [Fact(DisplayName = "Abrir — configuração congelada vazia é recusada")]
    public void Abrir_ConfiguracaoVazia_Lanca()
    {
        Action abrirSemBytes = () => VersaoConfiguracao.Abrir(
            Guid.CreateVersion7(),
            [],
            "1.0",
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            HashAto,
            "user-sub-123",
            TimeProvider.System);

        abrirSemBytes.Should().Throw<ArgumentException>()
            .WithParameterName("configuracaoCongeladaCanonica");
    }

    /// <summary>
    /// Relógio manual determinístico — permite simular o retrocesso do relógio
    /// do host (ajuste NTP em degrau) sem depender de
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c> na camada de teste do
    /// Domain, que só referencia Domain + Kernel.
    /// </summary>
    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}
