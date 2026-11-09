namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class DocumentosDasVersoesTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";
    private const string Manter = "autorizacao:perfis:manter";
    private const string AprovarProtecaoDeDados = "autorizacao:perfis:aprovar-protecao-de-dados";

    private static readonly CatalogoDeTeste Catalogo = new();

    [Fact(DisplayName = "Reemitir o envio devolve outra URL para a mesma chave do documento")]
    public async Task ReemitirEnvio_AguardandoEnvio_MesmaChave()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);

        Result<EnvioDoDocumentoDto> primeira = await cenario.Documentos.ReemitirEnvioAsync(perfil.Id, perfil.VersaoPendente!.Id, CancellationToken.None);
        Result<EnvioDoDocumentoDto> segunda = await cenario.Documentos.ReemitirEnvioAsync(perfil.Id, perfil.VersaoPendente.Id, CancellationToken.None);

        primeira.IsSuccess.Should().BeTrue();
        segunda.Value!.DocumentoId.Should().Be(primeira.Value!.DocumentoId);
        segunda.Value.UrlDeEnvio.Should().NotBe(primeira.Value.UrlDeEnvio);
        cenario.Armazenamento.ChavesDeEnvio.Distinct().Should().ContainSingle();
    }

    [Fact(DisplayName = "Reemitir o envio depois da confirmação é recusado")]
    public async Task ReemitirEnvio_AposConfirmar_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);
        await cenario.Documentos.ConfirmarAsync(perfil.Id, perfil.VersaoPendente!.Id, "etag", CancellationToken.None);

        Result<EnvioDoDocumentoDto> resultado = await cenario.Documentos.ReemitirEnvioAsync(perfil.Id, perfil.VersaoPendente.Id, CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.DocumentoNaoAguardaEnvio);
    }

    [Fact(DisplayName = "Novo documento depois da recusa emite uma chave diferente")]
    public async Task Novo_AposRecusa_EmiteOutraChave()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);
        DocumentoDaVersao recusado = perfil.VersaoPendente!.Documento!;
        perfil.ConfirmarDocumento(perfil.VersaoPendente.Id, "etag", PerfisDeTeste.Instante);
        perfil.RegistrarDocumentoRecusado(recusado.Id, PerfisDeTeste.Instante);

        Result<EnvioDoDocumentoDto> resultado = await cenario.Documentos.NovoAsync(perfil.Id, perfil.VersaoPendente.Id, 4096, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DocumentoId.Should().NotBe(recusado.Id);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Novo documento sem recusa é recusado e nada é gravado")]
    public async Task Novo_SemRecusa_NaoGrava()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);

        Result<EnvioDoDocumentoDto> resultado = await cenario.Documentos.NovoAsync(perfil.Id, perfil.VersaoPendente!.Id, 4096, CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.DocumentoNaoRecusado);
        cenario.Unidade.Gravacoes.Should().Be(0);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
    }

    [Fact(DisplayName = "Confirmar o envio põe o documento em verificação e o handler publica o pedido ao verificador")]
    public async Task Confirmar_PublicaOPedidoDeVerificacao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);

        (Result<DocumentoDaVersaoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarDocumentoDaVersaoCommandHandler.Handle(
            new ConfirmarDocumentoDaVersaoCommand(perfil.Id, perfil.VersaoPendente!.Id, "\"etag-1\""),
            cenario.Documentos,
            CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        resposta.Value!.Situacao.Should().Be("em_verificacao");
        PedidoDeVerificacaoDeArquivo pedido = mensagens.Should().ContainSingle().Subject.Should().BeOfType<PedidoDeVerificacaoDeArquivo>().Subject;
        DocumentoDaVersao documento = perfil.VersaoPendente.Documento!;
        pedido.ArquivoId.Should().Be(documento.Id);
        pedido.ChaveDeEnvio.Should().Be(documento.ChaveDeEnvio);
        pedido.ETag.Should().Be("\"etag-1\"");
        pedido.ChaveDefinitiva.Should().Be(documento.ChaveDefinitiva);
        pedido.TamanhoMaximoBytes.Should().Be(PerfisDeTeste.TamanhoDoDocumento);
        pedido.Destino.Should().Be(DestinoDoResultado.Autorizacao);
    }

    [Fact(DisplayName = "Confirmação recusada não publica pedido algum")]
    public async Task Confirmar_Recusada_NaoPublica()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);

        (Result<DocumentoDaVersaoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarDocumentoDaVersaoCommandHandler.Handle(
            new ConfirmarDocumentoDaVersaoCommand(perfil.Id, perfil.VersaoPendente!.Id, " "),
            cenario.Documentos,
            CancellationToken.None);

        resposta.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.DocumentoInvalido);
        mensagens.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Ler o documento verificado registra o acesso antes de devolver a URL")]
    public async Task Ler_Verificado_RegistraATrilha()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);
        DocumentoDaVersao documento = perfil.VersaoVigente!.Documento!;

        Result<LeituraDoDocumentoDaVersaoDto> resultado = await cenario.Documentos.LerAsync(perfil.Id, perfil.VersaoVigente.Id, cenario.Verificador, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Url.AbsoluteUri.Should().Contain(documento.ChaveDefinitiva);
        cenario.Armazenamento.ChavesDeLeitura.Should().Equal(documento.ChaveDefinitiva);
        LeituraDoDocumentoDaVersao leitura = cenario.Perfis.Leituras.Should().ContainSingle().Subject;
        leitura.DocumentoId.Should().Be(documento.Id);
        leitura.LeitorSub.Should().Be(PerfisDeTeste.Autor.Sub);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Ler documento ainda não verificado é recusado e não registra nem emite URL")]
    public async Task Ler_NaoVerificado_NaoRegistraNada()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil);

        Result<LeituraDoDocumentoDaVersaoDto> resultado = await cenario.Documentos.LerAsync(perfil.Id, perfil.VersaoPendente!.Id, cenario.Verificador, CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.DocumentoNaoVerificado);
        cenario.Perfis.Leituras.Should().BeEmpty();
        cenario.Armazenamento.ChavesDeLeitura.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Quem aprova pela proteção de dados lê o documento e a trilha registra o acesso")]
    public async Task Ler_ProtecaoDeDados_Permite()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil, true, AprovarProtecaoDeDados);

        (await cenario.Documentos.LerAsync(perfil.Id, perfil.VersaoVigente!.Id, cenario.Verificador, CancellationToken.None)).IsSuccess.Should().BeTrue();

        cenario.Verificador.Gravados.Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem nenhuma das duas permissões a leitura do documento é recusada, sem URL nem acesso gravado")]
    public async Task Ler_SemPermissao_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil, true, "autorizacao:perfis:aprovar-funcional");

        Result<LeituraDoDocumentoDaVersaoDto> resultado = await cenario.Documentos.LerAsync(perfil.Id, perfil.VersaoVigente!.Id, cenario.Verificador, CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        cenario.Verificador.Gravados.Should().ContainSingle();
        cenario.Armazenamento.ChavesDeLeitura.Should().BeEmpty();
        cenario.Perfis.Leituras.Should().BeEmpty();
    }

    [Fact(DisplayName = "Ler sem autor identificado ou de perfil inexistente é recusado")]
    public async Task Ler_SemAutorOuPerfilInexistente_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);

        Cenario comAutor = new(perfil);
        Cenario semAutor = new(perfil, autorIdentificado: false);

        (await comAutor.Documentos.LerAsync(Guid.NewGuid(), Guid.NewGuid(), comAutor.Verificador, CancellationToken.None))
            .Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        (await semAutor.Documentos.LerAsync(perfil.Id, perfil.VersaoVigente!.Id, semAutor.Verificador, CancellationToken.None))
            .Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
    }

    private sealed class Cenario
    {
        public Cenario(Perfil perfil, bool autorIdentificado = true, params string[] permitidas)
        {
            Perfis = new PerfisNaMemoria(perfil);
            Unidade = new UnidadeDeTrabalho();
            Armazenamento = new Armazenamento();
            Verificador = new VerificadorDeTeste(PerfisDeTeste.Autor.Sub, permitidas.Length == 0 ? [Manter] : permitidas);
            Documentos = new DocumentosDasVersoes(
                new OperacaoSobreOPerfil(Perfis, Unidade),
                new EnvioDoDocumento(Armazenamento, TimeProvider.System),
                new AutoriaDaOperacao(new Usuario(autorIdentificado ? PerfisDeTeste.Autor : null), TimeProvider.System, new ConcessoesNaMemoria()),
                Perfis,
                Armazenamento,
                Unidade);
        }

        public VerificadorDeTeste Verificador { get; }

        public PerfisNaMemoria Perfis { get; }

        public UnidadeDeTrabalho Unidade { get; }

        public Armazenamento Armazenamento { get; }

        public DocumentosDasVersoes Documentos { get; }
    }
}
