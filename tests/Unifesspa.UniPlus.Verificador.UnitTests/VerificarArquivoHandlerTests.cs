namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Armazenamento;
using Unifesspa.UniPlus.Verificador.Contratos;

public sealed class VerificarArquivoHandlerTests
{
    [Fact(DisplayName = "PDF com senha de abertura é recusado sem ir ao clamd")]
    public async Task PdfComSenhaDeAbertura_RecusaSemIrAoClamd()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("senha-de-abertura.pdf") };
        ClamdFalso clamd = new();

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Recusado);
        resultado.Motivo.Should().Be(MotivoDaRecusa.PdfComSenha);
        clamd.Verificacoes.Should().Be(0);
        armazenamento.Promocoes.Should().Be(0);
    }

    [Theory(DisplayName = "PDF sem senha ou só de proprietário é verificado e promovido")]
    [InlineData("sem-senha.pdf")]
    [InlineData("so-de-proprietario.pdf")]
    public async Task PdfQueAbreSemSenha_VerificaEPromove(string fixture)
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture(fixture) };
        ClamdFalso clamd = new();

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Verificado);
        resultado.HashSha256.Should().HaveLength(64);
        resultado.TamanhoBytes.Should().Be(armazenamento.Conteudo.Length);
        clamd.Verificacoes.Should().Be(1);
        armazenamento.Promocoes.Should().Be(1);
        armazenamento.Remocoes.Should().Be(1);
    }

    [Fact(DisplayName = "Reentrega de arquivo já promovido devolve o mesmo veredicto sem copiar de novo")]
    public async Task ReentregaJaPromovida_DevolveVerificadoSemPromoverDeNovo()
    {
        ArmazenamentoFalso armazenamento = new() { Definitivo = new ObjetoArmazenado("\"d\"", 2048, "abc123", Cenario.ETag), Envio = Cenario.EnvioComETag(Cenario.ETag) };
        ClamdFalso clamd = new();

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Verificado);
        resultado.HashSha256.Should().Be("abc123");
        armazenamento.Promocoes.Should().Be(0);
        armazenamento.Leituras.Should().Be(0);
        armazenamento.Remocoes.Should().Be(1);
        clamd.Verificacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Definitivo promovido sem SHA-256 no metadado é recusado, nunca devolvido como verificado")]
    public async Task DefinitivoSemHash_Recusa()
    {
        ArmazenamentoFalso armazenamento = new() { Definitivo = new ObjetoArmazenado("\"d\"", 2048, null, Cenario.ETag) };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso()).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Recusado);
        resultado.Motivo.Should().Be(MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
    }

    [Fact(DisplayName = "Erro permanente do clamd publica recusa de segurança em vez de deixar o arquivo em verificação")]
    public async Task ErroPermanenteDoClamd_RecusaPorSeguranca()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new() { Falha = new ClamdErroPermanenteException("INSTREAM size limit exceeded. ERROR") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Recusado);
        resultado.Motivo.Should().Be(MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        armazenamento.Promocoes.Should().Be(0);
    }

    [Fact(DisplayName = "Indisponibilidade do clamd propaga, para a política de erro tentar de novo")]
    public async Task ClamdIndisponivel_Propaga()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new() { Falha = new ClamdIndisponivelException("fora do ar") };
        Func<Task> acao = () => Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        await acao.Should().ThrowAsync<ClamdIndisponivelException>();
    }

    [Fact(DisplayName = "O Content-Type promovido vem do formato conferido")]
    public async Task Promocao_UsaContentTypeDoFormatoConferido()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };

        await Cenario.Handler(armazenamento, new ClamdFalso()).Handle(Cenario.Pedido(), CancellationToken.None);

        armazenamento.ContentTypePromovido.Should().Be("application/pdf");
    }

    [Fact(DisplayName = "Versão trocada por outro PUT resulta em reenvio necessário")]
    public async Task VersaoTrocada_ReenvioNecessario()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag("\"outra-versao\"") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso()).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.ReenvioNecessario);
        armazenamento.Leituras.Should().Be(0);
    }

    [Fact(DisplayName = "Versão que some entre a consulta e a leitura resulta em reenvio necessário")]
    public async Task VersaoQueSomeNaLeitura_ReenvioNecessario()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), VersaoLegivel = false };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso()).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.ReenvioNecessario);
    }

    [Fact(DisplayName = "Versão promovida por entrega duplicada entre a consulta e a leitura devolve verificado, e não reenvio")]
    public async Task PromovidaPorOutraEntregaNaLeitura_DevolveVerificado()
    {
        ArmazenamentoFalso armazenamento = new()
        {
            Envio = Cenario.EnvioComETag(Cenario.ETag),
            VersaoLegivel = false,
            DefinitivoAoLer = new ObjetoArmazenado("\"d\"", 2048, "abc123", Cenario.ETag),
        };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso()).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Verificado);
        resultado.HashSha256.Should().Be("abc123");
    }

    [Fact(DisplayName = "Arquivo acima do tamanho máximo é recusado sem ser lido")]
    public async Task TamanhoAcimaDoMaximo_RecusaSemLer()
    {
        long maximo = 1024;
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag, tamanho: maximo + 1) };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, new ClamdFalso())
            .Handle(Cenario.Pedido(maximo: maximo), CancellationToken.None);

        resultado.Veredicto.Should().Be(VeredictoDaVerificacao.Recusado);
        resultado.Motivo.Should().Be(MotivoDaRecusa.TamanhoExcedido);
        armazenamento.Leituras.Should().Be(0);
    }

    [Fact(DisplayName = "Conteúdo que não corresponde ao formato declarado é recusado sem ir ao clamd")]
    public async Task FormatoDiferenteDoDeclarado_Recusa()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new();

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd)
            .Handle(Cenario.Pedido(FormatoDoArquivo.Png), CancellationToken.None);

        resultado.Motivo.Should().Be(MotivoDaRecusa.FormatoNaoCorresponde);
        clamd.Verificacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Ameaça apontada pelo clamd recusa o arquivo e não o promove")]
    public async Task Infectado_RecusaSemPromover()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new() { Resposta = new ResultadoDoClamd(true, "Eicar-Test-Signature") };

        ResultadoDaVerificacaoDeArquivo resultado = await Cenario.Handler(armazenamento, clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        resultado.Motivo.Should().Be(MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca);
        resultado.HashSha256.Should().BeNull();
        armazenamento.Promocoes.Should().Be(0);
    }

    [Fact(DisplayName = "O resultado do arquivo promovido não carrega o nome da ameaça nem a chave do objeto")]
    public void Resultado_NaoTemCampoParaNomeDaAmeacaNemChave()
    {
        typeof(ResultadoDaVerificacaoDeArquivo).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(
                nameof(ResultadoDaVerificacaoDeArquivo.ArquivoId),
                nameof(ResultadoDaVerificacaoDeArquivo.Veredicto),
                nameof(ResultadoDaVerificacaoDeArquivo.Motivo),
                nameof(ResultadoDaVerificacaoDeArquivo.HashSha256),
                nameof(ResultadoDaVerificacaoDeArquivo.TamanhoBytes),
                nameof(ResultadoDaVerificacaoDeArquivo.Formato));
    }

    [Fact(DisplayName = "Falha do armazenamento propaga, para a política de erro tentar de novo")]
    public async Task FalhaDoArmazenamento_Propaga()
    {
        ArmazenamentoFalso armazenamento = new() { Envio = Cenario.EnvioComETag(Cenario.ETag), Conteudo = Cenario.Fixture("sem-senha.pdf") };
        ClamdFalso clamd = new();
        Func<Task> acao = () => Cenario.Handler(new ArmazenamentoQueFalha(armazenamento), clamd).Handle(Cenario.Pedido(), CancellationToken.None);

        await acao.Should().ThrowAsync<ArmazenamentoException>();
    }

    private sealed class ArmazenamentoQueFalha(ArmazenamentoFalso interno) : IArmazenamentoDeArquivos
    {
        public Task<ObjetoArmazenado?> ConsultarDefinitivoAsync(string chave, CancellationToken cancellationToken) =>
            interno.ConsultarDefinitivoAsync(chave, cancellationToken);

        public Task<ObjetoArmazenado?> ConsultarEnvioAsync(string chave, CancellationToken cancellationToken) =>
            throw new ArmazenamentoException("403");

        public Task<bool> LerEnvioAsync(string chave, string etag, Stream destino, CancellationToken cancellationToken) =>
            throw new ArmazenamentoException("403");

        public Task PromoverAsync(string chaveDeEnvio, string etag, string chaveDefinitiva, string contentType, string sha256, CancellationToken cancellationToken) =>
            throw new ArmazenamentoException("403");

        public Task RemoverEnvioAsync(string chave, string etag, CancellationToken cancellationToken) =>
            throw new ArmazenamentoException("403");
    }
}
