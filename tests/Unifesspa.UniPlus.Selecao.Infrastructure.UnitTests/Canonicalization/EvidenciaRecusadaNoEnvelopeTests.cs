namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class EvidenciaRecusadaNoEnvelopeTests
{
    [Theory]
    [InlineData(true, "EnvelopeCodec.VersaoDesconhecida", "A versão do envelope não é reconhecida.")]
    [InlineData(false, "EnvelopeCodec.AlgoritmoNaoSuportado", "A versão de configuração declara um algoritmo de hash não suportado.")]
    public void Reidratar_EvidenciaDesconhecida_NaoRepeteValor(bool versaoInvalida, string codigo, string mensagem)
    {
        const string valor = "ExemploEvidenciaDesconhecida";
        byte[] bytes = Encoding.UTF8.GetBytes($"{{\"valor\":\"{valor}\"}}");
        DocumentoCongelado documento = new(bytes, versaoInvalida ? valor : SnapshotPublicacaoCanonicalizer.SchemaVersionAtual,
            versaoInvalida ? PerfilCanonicoV1.Instancia.Algoritmo : valor, new string('a', 64));
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(Guid.NewGuid(), documento, Guid.NewGuid(), "usuario-ficticio", DateTimeOffset.UnixEpoch);
        FieldError causa = new EnvelopeCodec().Reidratar(versao).Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(codigo);
        causa.Error.Message.Should().Be(mensagem).And.NotContain(valor);
        causa.Field.Should().BeNull();
    }
}
