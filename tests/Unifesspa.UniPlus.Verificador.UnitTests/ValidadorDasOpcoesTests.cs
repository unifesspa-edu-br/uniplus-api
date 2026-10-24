namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Configuracao;

public sealed class ValidadorDasOpcoesTests
{
    private static VerificadorOptions Valida() => new()
    {
        Armazenamento = { Endpoint = "minio:9000", AccessKey = "a", SecretKey = "s", BucketEnvios = "envios", BucketArquivos = "arquivos" },
        RabbitMq = { Host = "rabbit" },
    };

    [Fact(DisplayName = "A configuração válida sobe")]
    public void ConfiguracaoValida_Sobe() =>
        new ValidadorDasOpcoes().Validate(null, Valida()).Succeeded.Should().BeTrue();

    [Fact(DisplayName = "O tamanho máximo no StreamMaxLength do clamd ou acima dele falha a subida")]
    public void TamanhoMaximoNoLimiteDoClamd_FalhaASubida()
    {
        VerificadorOptions opcoes = Valida();
        opcoes.TamanhoMaximoBytes = opcoes.Clamd.StreamMaxLengthBytes;

        new ValidadorDasOpcoes().Validate(null, opcoes).Failed.Should().BeTrue();
    }

    [Fact(DisplayName = "O paralelismo igual ao MaxThreads do clamd falha a subida, para sobrar thread ao PING")]
    public void ParalelismoNoMaxThreads_FalhaASubida()
    {
        VerificadorOptions opcoes = Valida();
        opcoes.Paralelismo = opcoes.Clamd.MaxThreads;

        new ValidadorDasOpcoes().Validate(null, opcoes).Failed.Should().BeTrue();
    }

    [Theory(DisplayName = "Porta e prazo do clamd menores ou iguais a zero falham a subida")]
    [InlineData(0, 60)]
    [InlineData(3310, 0)]
    public void PortaOuPrazoDoClamdInvalidos_FalhaASubida(int porta, int prazo)
    {
        VerificadorOptions opcoes = Valida();
        opcoes.Clamd.Port = porta;
        opcoes.Clamd.TimeoutSegundos = prazo;

        new ValidadorDasOpcoes().Validate(null, opcoes).Failed.Should().BeTrue();
    }
}
