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
        ErrorResponseException semChave = new(new ErrorResponse { Code = "NoSuchKey" }, new Minio.DataModel.Result.ResponseResult(new HttpRequestMessage(), new HttpResponseMessage()));
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
}
