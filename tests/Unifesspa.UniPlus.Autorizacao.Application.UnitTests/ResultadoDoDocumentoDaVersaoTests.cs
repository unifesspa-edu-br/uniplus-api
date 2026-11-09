namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Verificador.Contratos;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;
using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class ResultadoDoDocumentoDaVersaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly CatalogoDeTeste Catalogo = new();
    private static readonly DateTimeOffset Prazo = PerfisDeTeste.Instante.AddDays(15);

    [Fact(DisplayName = "Veredicto verificado só grava o documento e o hash: a versão segue pendente até a aprovação")]
    public async Task Verificado_SoGravaODocumento()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        documento.Sha256.Should().Be(Sha);
        perfil.VersaoPendente.Should().NotBeNull();
        perfil.VersaoVigente.Should().BeNull();
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Veredicto recusado devolve o documento a recusado e a versão segue pendente")]
    public async Task Recusado_DocumentoRecusadoVersaoPendente()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Recusado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Recusado);
        perfil.VersaoPendente.Should().NotBeNull();
        perfil.VersaoVigente.Should().BeNull();
    }

    [Fact(DisplayName = "Veredicto verificado depois do prazo expira a versão, que não entra em vigor, e registra o documento")]
    public async Task Verificado_DepoisDoPrazo_ExpiraARegistraODocumento()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil, Prazo.AddDays(1));

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Expirada);
        perfil.VersaoVigente.Should().BeNull();
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Veredicto ignorado depois do prazo grava a expiração")]
    public async Task Ignorado_DepoisDoPrazo_GravaAExpiracao()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        documento.RegistrarVerificado(Sha, PerfisDeTeste.Instante);
        Cenario cenario = new(perfil, Prazo);

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Expirada);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Resultado repetido é ignorado e não altera o documento")]
    public async Task Repetido_Ignora()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);
        await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        perfil.VersaoPendente.Should().NotBeNull();
    }

    [Fact(DisplayName = "Veredicto tardio de versão cancelada só grava o documento")]
    public async Task Tardio_VersaoCancelada_SoGravaODocumento()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        perfil.CancelarVersao(perfil.Versoes.Single().Id, PerfisDeTeste.Autor, "Versão proposta por engano.", PerfisDeTeste.Instante);
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        perfil.Versoes.Single().Situacao.Should().Be(SituacaoDaVersao.Cancelada);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
    }

    [Fact(DisplayName = "Arquivo que não é documento de versão é desconhecido")]
    public async Task ArquivoDesconhecido_Desconhecido()
    {
        Cenario cenario = new(PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo));

        DesfechoDoResultadoDaVersao desfecho = await cenario.Resultado.RegistrarAsync(Verificado(Guid.NewGuid()), CancellationToken.None);

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Desconhecido);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    private static (Perfil Perfil, DocumentoDaVersao Documento) EmVerificacao()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(
            perfil.ConfirmarDocumento(perfil.VersaoPendente!.Id, "etag", PerfisDeTeste.Instante));

        return (perfil, documento);
    }

    private static ResultadoDaVerificacaoDeArquivo Verificado(Guid arquivoId) =>
        new(arquivoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, Sha, 1024, FormatoDoArquivo.Pdf);

    private static ResultadoDaVerificacaoDeArquivo Recusado(Guid arquivoId) =>
        new(arquivoId, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null);

    private sealed class Cenario
    {
        public Cenario(Perfil perfil)
            : this(perfil, PerfisDeTeste.Instante.AddHours(1))
        {
        }

        public Cenario(Perfil perfil, DateTimeOffset agora)
        {
            Unidade = new UnidadeDeTrabalho();
            Resultado = DoublesDeAvisos.Versoes(new PerfisNaMemoria(perfil), Unidade, new RelogioFixo(agora));
        }

        public UnidadeDeTrabalho Unidade { get; }

        public ResultadoDoDocumentoDaVersao Resultado { get; }
    }
}
