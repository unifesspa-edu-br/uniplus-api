namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// Os gates da reidratação perguntam ao <b>perfil da versão</b>, e não a um serializador global.
/// </summary>
/// <remarks>
/// <para>
/// Enquanto houver um só perfil, as duas coisas coincidem — e é justamente por coincidirem que
/// a diferença não aparece em teste nenhum das suítes existentes. No dia do primeiro perfil
/// novo, um gate que ainda perguntasse ao global recusaria como "fora da forma canônica" um
/// envelope perfeitamente válido, ou aceitaria como canônico um que não é.
/// </para>
/// <para>
/// O registro é montado aqui com codecs próprios, pelo construtor interno. Não é atalho de
/// teste: é a única maneira de exercitar um perfil <b>diferente</b> antes de existir um.
/// </para>
/// </remarks>
public sealed class RegistroCodecsEnvelopePerfilTests
{
    private const string VersaoDeTeste = "9.9";

    /// <summary>
    /// Perfil que serializa <b>indentado</b>. Os bytes que ele produz nunca coincidem com os do
    /// perfil v1 para o mesmo payload — que é exatamente o que o torna um detector.
    /// </summary>
    private sealed class PerfilIndentado : IPerfilCanonico
    {
        public string Algoritmo => "canonical-json-indentado/sha256@teste";

        public byte[] Serializar(JsonObject payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Encoding.UTF8.GetBytes(payload.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        public string HashHex(byte[] bytes) => PerfilCanonicoV1.Instancia.HashHex(bytes);
    }

    /// <summary>Perfil que recusa <c>null</c> — a estrita que a versão seguinte do envelope terá.</summary>
    private sealed class PerfilQueRecusaNulo : IPerfilCanonico
    {
        public string Algoritmo => "canonical-json-sem-nulo/sha256@teste";

        public byte[] Serializar(JsonObject payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (payload.Any(static par => par.Value is null))
            {
                throw new PayloadForaDoPerfilCanonicoException(
                    "'null' não é representável neste perfil — a ausência do dado se declara antes de canonicalizar.");
            }

            return PerfilCanonicoV1.Instancia.Serializar(payload);
        }

        public string HashHex(byte[] bytes) => PerfilCanonicoV1.Instancia.HashHex(bytes);
    }

    private sealed class CodecDeTeste(IPerfilCanonico perfil) : IEnvelopeCodec
    {
        public string SchemaVersion => VersaoDeTeste;

        public IPerfilCanonico Perfil { get; } = perfil;

        public string AlgoritmoHash => Perfil.Algoritmo;

        public bool TemEncoder => true;

        public bool TemDecoder => true;

        public string? MotivoDaRecusa => null;

        public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada) =>
            new(Perfil.Serializar(PayloadQualquer()), SchemaVersion, AlgoritmoHash);

        /// <summary>Chegar aqui já significa que os gates passaram — é o que os testes observam.</summary>
        public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao) =>
            Result<EnvelopeReidratado>.Failure(new DomainError(
                "SELECAO_CODEC_DE_TESTE_ALCANCADO", "Os gates do registro deixaram passar."));
    }

    private static JsonObject PayloadQualquer() => new() { ["a"] = 1 };

    private static VersaoConfiguracao VersaoCom(IPerfilCanonico perfil, byte[] bytes) =>
        VersaoConfiguracao.Abrir(
            processoSeletivoId: Guid.CreateVersion7(),
            configuracaoCongeladaCanonica: bytes,
            schemaVersion: VersaoDeTeste,
            algoritmoHash: perfil.Algoritmo,
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorHash: new string('a', 64),
            atorUsuarioSub: "user-sub-1",
            instante: DateTimeOffset.UnixEpoch);

    private static RegistroCodecsEnvelope RegistroCom(IPerfilCanonico perfil) =>
        new([new CodecDeTeste(perfil)], VersaoDeTeste);

    /// <summary>
    /// Os bytes indentados <b>não</b> são canônicos sob o perfil v1 — mas são sob o perfil desta
    /// versão. Um gate preso ao serializador global recusaria aqui; o gate correto deixa passar
    /// e falha adiante, no decoder.
    /// </summary>
    [Fact(DisplayName = "Gate de forma — bytes canônicos sob o perfil DA VERSÃO passam, mesmo não sendo canônicos sob o v1")]
    public void GateDeForma_UsaOPerfilDaVersao()
    {
        PerfilIndentado perfil = new();
        byte[] bytes = perfil.Serializar(PayloadQualquer());

        bytes.Should().NotEqual(PerfilCanonicoV1.Instancia.Serializar(PayloadQualquer()),
            "o teste não prova nada se os dois perfis produzirem os mesmos bytes");

        Result<EnvelopeReidratado> resultado = RegistroCom(perfil).Reidratar(VersaoCom(perfil, bytes));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("SELECAO_CODEC_DE_TESTE_ALCANCADO",
            "os três gates — algoritmo, hash e forma — têm de ter passado para a execução chegar ao decoder");
    }

    /// <summary>O contrário: bytes canônicos sob o v1, mas não sob o perfil declarado pela versão.</summary>
    [Fact(DisplayName = "Gate de forma — bytes canônicos sob o v1 são recusados quando a versão declara outro perfil")]
    public void GateDeForma_RecusaBytesDeOutroPerfil()
    {
        PerfilIndentado perfil = new();
        byte[] bytesV1 = PerfilCanonicoV1.Instancia.Serializar(PayloadQualquer());

        Result<EnvelopeReidratado> resultado = RegistroCom(perfil).Reidratar(VersaoCom(perfil, bytesV1));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.IntegridadeViolada);
    }

    /// <summary>
    /// O que um perfil estrito recusa vira <b>recusa nomeada</b>, não falha não tratada — quem
    /// grava a coluna à mão consegue produzir exatamente o payload que ele não representa.
    /// </summary>
    [Fact(DisplayName = "Gate de forma — payload que o perfil recusa vira envelope malformado, não exceção")]
    public void GateDeForma_PayloadRecusadoPeloPerfil_ViraRecusaNomeada()
    {
        PerfilQueRecusaNulo perfil = new();
        byte[] comNulo = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["a"] = null });

        Result<EnvelopeReidratado> resultado = RegistroCom(perfil).Reidratar(VersaoCom(perfil, comNulo));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain(perfil.Algoritmo);
    }

    [Fact(DisplayName = "Registro — versão repetida entre codecs é recusada na construção")]
    public void Registro_RecusaVersaoRepetida()
    {
        Action montar = () => _ = new RegistroCodecsEnvelope(
            [new CodecDeTeste(new PerfilIndentado()), new CodecDeTeste(new PerfilIndentado())], VersaoDeTeste);

        montar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Registro — versão de emissão corrente sem codec é recusada na construção")]
    public void Registro_RecusaCorrenteSemCodec()
    {
        Action montar = () => _ = new RegistroCodecsEnvelope([new CodecDeTeste(new PerfilIndentado())], "8.8");

        montar.Should().Throw<ArgumentException>();
    }
}
