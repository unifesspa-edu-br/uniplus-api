namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Diagnostics.Metrics;
using System.Security.Cryptography;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Observabilidade;
using Unifesspa.UniPlus.Verificador.Verificacao;

public sealed class VerificarArquivoHandlerFalhasTests
{
    public static TheoryData<Exception, string> FalhasTransitorias => new()
    {
        { new ClamdIndisponivelException("fora do ar"), "clamd" },
        { new ArmazenamentoException("403"), "minio" },
    };

    [Theory(DisplayName = "Falha transitória propaga o mesmo erro e é contada na origem")]
    [MemberData(nameof(FalhasTransitorias))]
    public async Task Verificar_FalhaTransitoria_PropagaEContaNaOrigem(Exception falha, string origem)
    {
        ArmazenamentoFalso armazenamento = ArmazenamentoComPdfSemSenha();
        IArmazenamentoDeArquivos usado = falha is ArmazenamentoException ? new ArmazenamentoComFalhaNaPromocao(armazenamento, falha) : armazenamento;
        using CapturaDosErros erros = new();
        ClamdFalso clamd = new() { Falha = falha as ClamdException };
        VerificarArquivoHandler handler = Cenario.Handler(usado, clamd, erros.Metricas, new());
        Func<Task<ResultadoDaVerificacaoDeArquivo>> acao = () => handler.Verificar(Cenario.Pedido(), CancellationToken.None);

        await acao.Should().ThrowAsync<Exception>().Where(e => ReferenceEquals(e, falha));
        erros.Origens.Should().Equal(origem);
    }

    [Fact(DisplayName = "Erro permanente do clamd recusa por segurança e é contado como erro do clamd")]
    public async Task Verificar_ErroPermanenteDoClamd_RecusaEContaErroDoClamd()
    {
        using CapturaDosErros erros = new();
        ClamdFalso clamd = new() { Falha = new ClamdErroPermanenteException("resposta inesperada") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(ArmazenamentoComPdfSemSenha(), clamd, erros.Metricas, new())
            .Verificar(Cenario.Pedido(), CancellationToken.None);

        resultado.Motivo.Should().Be(MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        erros.Origens.Should().Equal("clamd");
    }

    [Fact(DisplayName = "Definitivo sem SHA-256 é recusado e contado como erro do armazenamento")]
    public async Task Verificar_DefinitivoSemHash_ContaErroDoMinio()
    {
        ArmazenamentoFalso armazenamento = new() { Definitivo = new ObjetoArmazenado("\"d\"", 2048, null, Cenario.ETag) };
        using CapturaDosErros erros = new();

        await Cenario.Handler(armazenamento, new ClamdFalso(), erros.Metricas, new()).Verificar(Cenario.Pedido(), CancellationToken.None);

        erros.Origens.Should().Equal("minio");
    }

    [Fact(DisplayName = "Definitivo de outra versão resulta em reenvio necessário")]
    public async Task Verificar_DefinitivoDeOutraVersao_ReenvioNecessario()
    {
        ArmazenamentoFalso armazenamento = new() { Definitivo = new ObjetoArmazenado("\"d\"", 2048, "abc123", "\"outra\"") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso()).Verificar(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.ReenvioNecessario);
        armazenamento.Remocoes.Should().Be(0);
    }

    [Fact(DisplayName = "O hash e o tamanho promovidos são os do arquivo inteiro, mesmo depois de o clamd ler o fluxo até o fim")]
    public async Task Verificar_ConteudoAprovado_PromoveOArquivoInteiro()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new();
        string esperado = Convert.ToHexStringLower(SHA256.HashData(armazenamento.Conteudo));

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Verificar(Cenario.Pedido(), CancellationToken.None);

        resultado.HashSha256.Should().Be(esperado);
        armazenamento.Sha256Promovido.Should().Be(esperado);
        armazenamento.ConteudoPromovido.Should().Equal(armazenamento.Conteudo);
        resultado.TamanhoBytes.Should().Be(armazenamento.Conteudo.Length);
        resultado.Formato.Should().Be(FormatoDoArquivo.Pdf);
        clamd.PrimeiroByteLido.Should().Be(armazenamento.Conteudo[0]);
    }

    [Fact(DisplayName = "PDF com senha é recusado antes de o antivírus ser consultado, mesmo que o antivírus apontasse ameaça")]
    public async Task Verificar_PdfComSenhaEAmeaca_RecusaPelaSenha()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("senha-de-abertura.pdf") };
        ClamdFalso clamd = new() { Resposta = new ResultadoDoClamd(true, "Eicar-Test-Signature") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Verificar(Cenario.Pedido(), CancellationToken.None);

        resultado.Motivo.Should().Be(MotivoDaRecusa.PdfComSenha);
        clamd.Verificacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Formato diferente do declarado é recusado antes da conferência de senha e do antivírus")]
    public async Task Verificar_FormatoDiferenteComSenhaEAmeaca_RecusaPeloFormato()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("senha-de-abertura.pdf") };
        ClamdFalso clamd = new() { Resposta = new ResultadoDoClamd(true, "Eicar-Test-Signature") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd)
            .Verificar(Cenario.Pedido(FormatoDoArquivo.Jpeg), CancellationToken.None);

        resultado.Motivo.Should().Be(MotivoDaRecusa.FormatoNaoCorresponde);
        clamd.Verificacoes.Should().Be(0);
    }

    [Theory(DisplayName = "O arquivo temporário é apagado ao fim da verificação, inclusive na falha")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verificar_FalhaOuSucesso_ApagaOArquivoTemporario(bool clamdFalha)
    {
        using DiretorioDeTeste diretorio = new();
        ClamdFalso clamd = new() { Falha = clamdFalha ? new ClamdIndisponivelException("fora do ar") : null };
        VerificadorOptions opcoes = new() { DiretorioTemporario = diretorio.Caminho };
        VerificarArquivoHandler handler = Cenario.Handler(ArmazenamentoComPdfSemSenha(), clamd, new MetricasDoVerificador(TimeProvider.System), opcoes);

        await Record.ExceptionAsync(() => handler.Verificar(Cenario.Pedido(), CancellationToken.None));

        clamd.Verificacoes.Should().Be(1);
        Directory.EnumerateFileSystemEntries(diretorio.Caminho).Should().BeEmpty();
    }

    private static ArmazenamentoFalso ArmazenamentoComPdfSemSenha() =>
        new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };

    private sealed class ArmazenamentoComFalhaNaPromocao(ArmazenamentoFalso interno, Exception falha) : IArmazenamentoDeArquivos
    {
        public Task<ObjetoArmazenado?> ConsultarDefinitivoAsync(string chave, CancellationToken cancellationToken) =>
            interno.ConsultarDefinitivoAsync(chave, cancellationToken);

        public Task<ObjetoArmazenado?> ConsultarEnvioAsync(string chave, CancellationToken cancellationToken) =>
            interno.ConsultarEnvioAsync(chave, cancellationToken);

        public Task<bool> LerEnvioAsync(string chave, string etag, Stream destino, CancellationToken cancellationToken) =>
            interno.LerEnvioAsync(chave, etag, destino, cancellationToken);

        public Task PromoverAsync(string chaveDeEnvio, string etag, string chaveDefinitiva, string contentType, string sha256, CancellationToken cancellationToken) =>
            Task.FromException(falha);

        public Task RemoverEnvioAsync(string chave, string etag, CancellationToken cancellationToken) =>
            interno.RemoverEnvioAsync(chave, etag, cancellationToken);
    }

    private sealed class DiretorioDeTeste : IDisposable
    {
        public string Caminho { get; } = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"verificador-teste-{Guid.NewGuid():N}")).FullName;

        public void Dispose() => Directory.Delete(Caminho, recursive: true);
    }

    // O medidor é estático por nome e os testes rodam em paralelo: só o instrumento criado por esta captura, na
    // thread dela, é escutado.
    private sealed class CapturaDosErros : IDisposable
    {
        private readonly MeterListener _escuta = new();
        private readonly int _threadDaCriacao = Environment.CurrentManagedThreadId;

        public CapturaDosErros()
        {
            _escuta.InstrumentPublished = (instrumento, escuta) =>
            {
                if (instrumento.Name == "uniplus.verificador.erros" && Environment.CurrentManagedThreadId == _threadDaCriacao)
                {
                    escuta.EnableMeasurementEvents(instrumento);
                }
            };
            _escuta.SetMeasurementEventCallback<long>((_, _, etiquetas, _) =>
                Origens.Add((string)etiquetas.ToArray().Single(e => e.Key == "origem").Value!));
            _escuta.Start();
            Metricas = new MetricasDoVerificador(TimeProvider.System);
        }

        public MetricasDoVerificador Metricas { get; }

        public List<string> Origens { get; } = [];

        public void Dispose()
        {
            _escuta.Dispose();
            Metricas.Dispose();
        }
    }
}
