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

    [Fact(DisplayName = "A configuração com todas as seções inválidas devolve todas as mensagens, na ordem das seções")]
    public void Validate_TodasAsSecoesInvalidas_DevolveAsMensagensNaOrdemDasSecoes()
    {
        VerificadorOptions opcoes = new() { TamanhoMaximoBytes = 0, Paralelismo = 0, Clamd = { TimeoutSegundos = 0, Port = 0 } };

        new ValidadorDasOpcoes().Validate(null, opcoes).Failures.Should().Equal(
            "O tamanho máximo do arquivo deve ser maior que zero.",
            "O paralelismo deve ser de pelo menos 1.",
            "O prazo do clamd deve ser maior que zero.",
            "A porta do clamd deve ser maior que zero.",
            "O endereço do MinIO é obrigatório.",
            "A chave de acesso do MinIO é obrigatória.",
            "A chave secreta do MinIO é obrigatória.",
            "O bucket de envio é obrigatório.",
            "O bucket definitivo é obrigatório.",
            "O endereço do RabbitMQ é obrigatório.");
    }

    [Fact(DisplayName = "Os limites que dependem do clamd devolvem a mensagem do próprio limite")]
    public void Validate_LimitesDependentesDoClamd_DevolveAsMensagensDoLimite()
    {
        VerificadorOptions opcoes = Valida();
        opcoes.TamanhoMaximoBytes = opcoes.Clamd.StreamMaxLengthBytes;
        opcoes.Paralelismo = opcoes.Clamd.MaxThreads;

        new ValidadorDasOpcoes().Validate(null, opcoes).Failures.Should().Equal(
            "O tamanho máximo do arquivo deve ficar abaixo do StreamMaxLength do clamd.",
            "O paralelismo deve ficar abaixo do MaxThreads do clamd.");
    }

    [Fact(DisplayName = "A URI do RabbitMQ inválida falha a subida mesmo com o host preenchido")]
    public void Validate_UriDoRabbitInvalida_FalhaASubida()
    {
        VerificadorOptions opcoes = Valida();
        opcoes.RabbitMq.Uri = "não é uma uri";

        new ValidadorDasOpcoes().Validate(null, opcoes).Failures.Should().Equal("A URI do RabbitMQ é inválida.");
    }

    [Fact(DisplayName = "A URI do RabbitMQ válida dispensa o host")]
    public void Validate_UriDoRabbitValida_DispensaOHost()
    {
        VerificadorOptions opcoes = Valida();
        opcoes.RabbitMq.Host = string.Empty;
        opcoes.RabbitMq.Uri = "amqp://rabbit:5672";

        new ValidadorDasOpcoes().Validate(null, opcoes).Succeeded.Should().BeTrue();
    }
}
