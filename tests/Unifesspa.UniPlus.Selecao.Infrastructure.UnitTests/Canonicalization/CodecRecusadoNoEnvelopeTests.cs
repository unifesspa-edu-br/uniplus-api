namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CodecRecusadoNoEnvelopeTests
{
    [Theory]
    [InlineData(false, "Os bytes congelados devem ser um JSON válido e sem chaves duplicadas.")]
    [InlineData(true, "Os bytes congelados contêm dados que o perfil canônico não representa.")]
    public void Parsear_ConteudoRecusado_NaoRepeteValor(bool perfilInvalido, string mensagem)
    {
        const string valor = "ExemploConteudoRecusado";
        IPerfilCanonico perfil = new PerfilRecusado(valor);
        byte[] bytes = Encoding.UTF8.GetBytes(perfilInvalido ? $"{{\"valor\":\"{valor}\"}}" : $"{{\"valor\":\"{valor}");
        Result<JsonObject> resultado = EnvelopeCodec.Parsear(perfil, bytes);
        FieldError causa = resultado.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be(mensagem).And.NotContain(valor);
        causa.Field.Should().BeNull();
    }
    private sealed class PerfilRecusado(string valor) : IPerfilCanonico
    {
        public string Algoritmo => "exemplo";
        public byte[] Serializar(JsonObject payload) => throw new PayloadForaDoPerfilCanonicoException(valor);
        public string HashHex(byte[] bytes) => throw new NotSupportedException();
    }
}
