namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

using NSubstitute;

using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;

public sealed class ArmazenamentoMinioTests
{
    public static TheoryData<Exception> FalhasDeRedeEDePrazo => new()
    {
        new HttpRequestException("rede"),
        new IOException("conexão interrompida"),
        new TaskCanceledException("prazo do SDK"),
    };

    [Theory(DisplayName = "Falha de rede ou prazo do SDK vira falha do armazenamento, que entra nas novas tentativas")]
    [MemberData(nameof(FalhasDeRedeEDePrazo))]
    public async Task FalhaDeRedeOuPrazo_ViraArmazenamentoException(Exception falha)
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        minio.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ObjectStat>(falha));
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions()));

        Func<Task> acao = () => armazenamento.ConsultarEnvioAsync("chave", CancellationToken.None);

        await acao.Should().ThrowAsync<ArmazenamentoException>();
    }

    [Fact(DisplayName = "Chave inexistente (NoSuchKey) é objeto ausente, e não falha do armazenamento")]
    public async Task ChaveInexistente_RetornaNulo()
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        using HttpRequestMessage requisicao = new();
        using HttpResponseMessage resposta = new();
        using Minio.DataModel.Result.ResponseResult resultado = new(requisicao, resposta);
        ErrorResponseException semChave = new(new ErrorResponse { Code = "NoSuchKey" }, resultado);
        minio.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ObjectStat>(semChave));
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions()));

        (await armazenamento.ConsultarDefinitivoAsync("chave", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "Cancelamento pedido pelo chamador segue como cancelamento")]
    public void CancelamentoDoChamador_NaoEFalhaDoArmazenamento()
    {
        using CancellationTokenSource cancelado = new();
        cancelado.Cancel();

        ArmazenamentoMinio.EhFalhaDoArmazenamento(new TaskCanceledException(), cancelado.Token).Should().BeFalse();
    }

    public static TheoryData<string, string> MensagensPorOperacao => new()
    {
        { "ler", "Falha ao ler o envio no armazenamento (HttpRequestException)." },
        { "promover", "Falha ao promover o arquivo no armazenamento (HttpRequestException)." },
        { "remover", "Falha ao remover o envio no armazenamento (HttpRequestException)." },
        { "consultar", "Falha ao consultar o armazenamento (HttpRequestException)." },
    };

    [Theory(DisplayName = "Cada operação traduz a falha de rede na mensagem da própria ação")]
    [MemberData(nameof(MensagensPorOperacao))]
    public async Task Operacao_FalhaDeRede_LancaArmazenamentoExceptionComAMensagemDaAcao(string operacao, string mensagem)
    {
        IMinioClient minio = MinioComFalhaDeRede(operacao);
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions()));

        Func<Task> acao = () => Executar(armazenamento, operacao);

        (await acao.Should().ThrowAsync<ArmazenamentoException>()).WithMessage(mensagem).WithInnerException<HttpRequestException>();
    }

    [Theory(DisplayName = "Na leitura do envio, objeto ausente e pré-condição falha são versão ausente, e não falha do armazenamento")]
    [InlineData("NoSuchKey")]
    [InlineData("PreconditionFailed")]
    public async Task LerEnvioAsync_ObjetoAusenteOuPreCondicaoFalha_RetornaFalso(string codigo)
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        minio.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ObjectStat>(RespostaDeErro(codigo)));
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions()));

        (await armazenamento.LerEnvioAsync("chave", "etag", Stream.Null, CancellationToken.None)).Should().BeFalse();
    }

    [Fact(DisplayName = "Na consulta, pré-condição falha não é objeto ausente e vira falha do armazenamento")]
    public async Task ConsultarEnvioAsync_PreCondicaoFalha_LancaArmazenamentoException()
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        minio.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ObjectStat>(RespostaDeErro("PreconditionFailed")));
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions()));

        Func<Task> acao = () => armazenamento.ConsultarEnvioAsync("chave", CancellationToken.None);

        await acao.Should().ThrowAsync<ArmazenamentoException>();
    }

    [Fact(DisplayName = "A promoção copia do envio condicionada ao ETag, com o Content-Type, o SHA-256 e o ETag do envio sem aspas no metadado")]
    public async Task PromoverAsync_ComEtagDoEnvio_CopiaDaOrigemCondicionadaAoEtag()
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        CopyObjectArgs? copia = null;
        minio.CopyObjectAsync(Arg.Do<CopyObjectArgs>(a => copia = a), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        ArmazenamentoMinio armazenamento = new(minio, Options.Create(new VerificadorOptions { Armazenamento = { BucketEnvios = "envios", BucketArquivos = "arquivos" } }));

        await armazenamento.PromoverAsync("envio/chave", "\"etag-1\"", "definitivo/chave", "application/pdf", "abc123", CancellationToken.None);

        Propriedade<string>(copia!, "BucketName").Should().Be("arquivos");
        Propriedade<string>(copia!, "ObjectName").Should().Be("definitivo/chave");
        Propriedade<Dictionary<string, string>>(copia!, "Headers").Should()
            .Contain("Content-Type", "application/pdf").And.Contain("x-amz-meta-sha256", "abc123").And.Contain("x-amz-meta-etag-envio", "etag-1");
        ConferirOrigemCondicionadaAoEtag(copia!);
    }

    private static void ConferirOrigemCondicionadaAoEtag(CopyObjectArgs copia)
    {
        object origem = Propriedade<object>(copia, "SourceObject");
        Propriedade<string>(origem, "BucketName").Should().Be("envios");
        Propriedade<string>(origem, "ObjectName").Should().Be("envio/chave");
        Propriedade<string>(origem, "MatchETag").Should().Be("\"etag-1\"");
    }

    private static T Propriedade<T>(object argumentos, string nome) =>
        (T)argumentos.GetType().GetProperty(nome, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(argumentos)!;

    private static Task Executar(ArmazenamentoMinio armazenamento, string operacao) => operacao switch
    {
        "ler" => armazenamento.LerEnvioAsync("chave", "\"etag\"", Stream.Null, CancellationToken.None),
        "promover" => armazenamento.PromoverAsync("envio", "\"etag\"", "definitivo", "application/pdf", "abc", CancellationToken.None),
        "remover" => armazenamento.RemoverEnvioAsync("chave", "\"etag\"", CancellationToken.None),
        _ => armazenamento.ConsultarEnvioAsync("chave", CancellationToken.None),
    };

    // A remoção só chega ao SDK depois de a consulta do envio confirmar o mesmo ETag.
    private static IMinioClient MinioComFalhaDeRede(string operacao)
    {
        IMinioClient minio = Substitute.For<IMinioClient>();
        HttpRequestException falha = new("rede");
        minio.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ObjectStat>(falha));
        minio.CopyObjectAsync(Arg.Any<CopyObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(falha));
        minio.RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(falha));
        Task<ObjectStat> consulta = operacao == "remover"
            ? Task.FromResult(ObjectStat.FromResponseHeaders("chave", new Dictionary<string, string>(StringComparer.Ordinal) { ["ETag"] = "\"etag\"" }))
            : Task.FromException<ObjectStat>(falha);
        minio.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>()).Returns(consulta);
        return minio;
    }

    private static ErrorResponseException RespostaDeErro(string codigo)
    {
        using HttpRequestMessage requisicao = new();
        using HttpResponseMessage resposta = new();
        using Minio.DataModel.Result.ResponseResult resultado = new(requisicao, resposta);
        return new ErrorResponseException(new ErrorResponse { Code = codigo }, resultado);
    }
}
