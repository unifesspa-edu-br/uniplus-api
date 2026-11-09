namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Verificador.Contratos;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class ResultadoDoDocumentoDaVersaoTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Descricao = "Descrição válida.";
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly CatalogoDeTeste Catalogo = new();

    [Fact(DisplayName = "Veredicto verificado da primeira versão põe o perfil em vigor e grava")]
    public async Task Verificado_PrimeiraVersao_EntraEmVigor()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = (await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None)).Desfecho;

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        perfil.VersaoVigente.Should().NotBeNull();
        perfil.Vigencias.Should().ContainSingle(v => v.Aberta);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Veredicto recusado devolve o documento a recusado e a versão segue pendente")]
    public async Task Recusado_DocumentoRecusadoVersaoPendente()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = (await cenario.Resultado.RegistrarAsync(Recusado(documento.Id), CancellationToken.None)).Desfecho;

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Recusado);
        perfil.VersaoPendente.Should().NotBeNull();
        perfil.VersaoVigente.Should().BeNull();
    }

    [Fact(DisplayName = "Resultado repetido é ignorado e não grava de novo")]
    public async Task Repetido_Ignora()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Cenario cenario = new(perfil);
        await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        DesfechoDoResultadoDaVersao desfecho = (await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None)).Desfecho;

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        cenario.Unidade.Gravacoes.Should().Be(1);
        perfil.Vigencias.Should().ContainSingle();
    }

    [Fact(DisplayName = "Veredicto tardio de versão cancelada só grava o documento")]
    public async Task Tardio_VersaoCancelada_SoGravaODocumento()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        perfil.CancelarVersao(perfil.Versoes.Single().Id, PerfisDeTeste.Autor, PerfisDeTeste.Instante);
        Cenario cenario = new(perfil);

        DesfechoDoResultadoDaVersao desfecho = (await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None)).Desfecho;

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        documento.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
    }

    [Fact(DisplayName = "Arquivo que não é documento de versão é desconhecido")]
    public async Task ArquivoDesconhecido_Desconhecido()
    {
        Cenario cenario = new(PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo));

        DesfechoDoResultadoDaVersao desfecho = (await cenario.Resultado.RegistrarAsync(Verificado(Guid.NewGuid()), CancellationToken.None)).Desfecho;

        desfecho.Should().Be(DesfechoDoResultadoDaVersao.Desconhecido);
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Nome tomado por outro perfil enquanto o documento esperava recusa a versão no veredicto")]
    public async Task Verificado_NomeTomado_RecusaAVersao()
    {
        (Perfil perfil, DocumentoDaVersao documento) = EmVerificacao();
        Perfil outro = PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo);
        Cenario cenario = new(perfil, outro);

        await cenario.Resultado.RegistrarAsync(Verificado(documento.Id), CancellationToken.None);

        perfil.UltimaVersaoRecusada!.MotivoDaRecusa.Should().Be(MotivoDaRecusaDaVersao.NomeIndisponivel);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
        cenario.Unidade.Gravacoes.Should().Be(1);
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
        public Cenario(params Perfil[] perfis)
        {
            Unidade = new UnidadeDeTrabalho();
            PerfisNaMemoria repositorio = new(perfis);
            Resultado = new ResultadoDoDocumentoDaVersao(
                repositorio,
                new LeitorDoContextoDaVersao(repositorio, new ConcessoesNaMemoria()),
                new EfeitosDaTrocaDeVigencia(new ConcessoesNaMemoria(), new PedidosAtivosNaMemoria(), DoublesDeAvisos.Avisos(), TimeProvider.System),
                Unidade,
                Catalogo,
                TimeProvider.System);
        }

        public UnidadeDeTrabalho Unidade { get; }

        public ResultadoDoDocumentoDaVersao Resultado { get; }
    }
}
