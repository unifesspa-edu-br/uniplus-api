namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ManifestacaoDeInteresseNoEnvelopeTests
{
    [Fact(DisplayName = "Serializar escreve o código de contrato da manifestação")]
    public void Serializar_EscreveOCodigo()
    {
        SerializacaoDaManifestacaoDeInteresse.Serializar(ManifestacaoDeInteresse.UmaVez)!.GetValue<string>().Should().Be("UMA_VEZ");
        SerializacaoDaManifestacaoDeInteresse.Serializar(ManifestacaoDeInteresse.ConfirmaACadaChamada)!.GetValue<string>().Should().Be("CONFIRMA_A_CADA_CHAMADA");
    }

    [Fact(DisplayName = "Sem declaração, o bloco é nulo e a chave continua presente no envelope")]
    public void Serializar_NaoPrevista_EscreveNulo()
    {
        JsonObject envelope = new() { ["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(null) };

        envelope.Should().ContainKey("manifestacaoDeInteresse").WhoseValue.Should().BeNull();
    }

    [Theory(DisplayName = "Ler reconstrói a manifestação a partir do bloco serializado, inclusive a não prevista")]
    [InlineData(null)]
    [InlineData(ManifestacaoDeInteresse.UmaVez)]
    [InlineData(ManifestacaoDeInteresse.ConfirmaACadaChamada)]
    public void Ler_BlocoSerializado_Reconstroi(ManifestacaoDeInteresse? declarada)
    {
        JsonObject envelope = new() { ["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(declarada) };
        LeitorEnvelope leitor = new();

        ManifestacaoDeInteresse? lida = LeituraDaManifestacaoDeInteresse.Ler(leitor, envelope);

        leitor.Falhou.Should().BeFalse();
        lida.Should().Be(declarada);
    }

    [Fact(DisplayName = "A chave ausente no envelope é lida como manifestação não prevista, e quem recusa a ausência é a conferência das chaves do envelope")]
    public void Ler_ChaveAusente_NaoPrevista()
    {
        LeitorEnvelope leitor = new();

        ManifestacaoDeInteresse? lida = LeituraDaManifestacaoDeInteresse.Ler(leitor, []);

        leitor.Falhou.Should().BeFalse();
        lida.Should().BeNull();
    }

    [Fact(DisplayName = "A manifestação que não é texto recusa o envelope")]
    public void Ler_TipoErrado_Recusa()
    {
        LeitorEnvelope leitor = new();

        LeituraDaManifestacaoDeInteresse.Ler(leitor, new JsonObject { ["manifestacaoDeInteresse"] = 7 });

        leitor.Falhou.Should().BeTrue();
    }

    [Fact(DisplayName = "O código desconhecido é recusado pelo domínio e aponta o campo no envelope, sem repetir o valor")]
    public void Ler_CodigoDesconhecido_ApontaOCampo()
    {
        JsonObject documento = new() { ["manifestacaoDeInteresse"] = "VALOR_SECRETO_9876" };
        LeitorEnvelope leitor = new();

        LeituraDaManifestacaoDeInteresse.Ler(leitor, documento);

        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(static causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido));
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_SECRETO_9876", StringComparison.Ordinal));
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
    }

    [Fact(DisplayName = "O hash do envelope muda quando a manifestação muda")]
    public void Hash_ManifestacaoAlterada_MudaResumo()
    {
        JsonObject envelope = new() { ["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(ManifestacaoDeInteresse.UmaVez) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(ManifestacaoDeInteresse.ConfirmaACadaChamada);

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
    }

    [Fact(DisplayName = "O hash do envelope muda quando a manifestação passa de não prevista a prevista")]
    public void Hash_NaoPrevistaParaPrevista_MudaResumo()
    {
        JsonObject envelope = new() { ["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(null) };
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        string original = perfil.HashHex(perfil.Serializar(envelope));

        envelope["manifestacaoDeInteresse"] = SerializacaoDaManifestacaoDeInteresse.Serializar(ManifestacaoDeInteresse.UmaVez);

        perfil.HashHex(perfil.Serializar(envelope)).Should().NotBe(original);
    }
}
