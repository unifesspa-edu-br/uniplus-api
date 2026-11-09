namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDePerfis;

public sealed class VersoesDoPerfilTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Publicar = "selecao:processos:publicar";
    private const string Descricao = "Descrição válida.";

    private static readonly CatalogoDeTeste Catalogo = new();

    [Fact(DisplayName = "Criar o perfil grava o perfil com a versão 1 pendente e devolve a URL de envio do documento")]
    public async Task CriarPerfil_Valido_GravaEDevolveAUrl()
    {
        Cenario cenario = new();

        Result<EnvioDoDocumentoDto> resultado = await cenario.Versoes.CriarPerfilAsync(Dados("Analista"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        Perfil perfil = cenario.Perfis.Unico();
        perfil.VersaoPendente!.Documento!.Id.Should().Be(resultado.Value!.DocumentoId);
        resultado.Value.PerfilId.Should().Be(perfil.Id);
        resultado.Value.VersaoId.Should().Be(perfil.VersaoPendente.Id);
        resultado.Value.Situacao.Should().Be("aguardando_envio");
        resultado.Value.TipoDeMidia.Should().Be("application/pdf");
        cenario.Armazenamento.ChavesDeEnvio.Should().Equal(perfil.VersaoPendente.Documento.ChaveDeEnvio);
        cenario.Unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Dados inválidos são recusados por inteiro, sem emitir URL nem gravar")]
    public async Task CriarPerfil_Invalido_NaoEmiteNemGrava()
    {
        Cenario cenario = new();

        Result<EnvioDoDocumentoDto> resultado = await cenario.Versoes.CriarPerfilAsync(
            new DadosDaVersaoRequest(" ", "ab", [], " ", 0), CancellationToken.None);

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
            PerfilErrorCodes.NomeInvalido,
            PerfilErrorCodes.DescricaoInvalida,
            PerfilErrorCodes.ProtocoloInvalido,
            PerfilErrorCodes.SemPermissao,
            PerfilErrorCodes.DocumentoInvalido);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
        cenario.Unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Nome já usado por outro perfil é recusado antes de emitir a URL")]
    public async Task CriarPerfil_NomeEmUso_Recusa()
    {
        Cenario cenario = new(PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo));

        Result<EnvioDoDocumentoDto> resultado = await cenario.Versoes.CriarPerfilAsync(Dados("  analista  "), CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NomeDuplicado);
        cenario.Armazenamento.ChavesDeEnvio.Should().BeEmpty();
    }

    [Fact(DisplayName = "Corrida de nome no índice único vira recusa de nome duplicado e descarta o rastreamento")]
    public async Task CriarPerfil_IndiceUnicoDoNome_NomeDuplicado()
    {
        Cenario cenario = new(falha: new Microsoft.EntityFrameworkCore.DbUpdateException("duplicada", new ViolacaoDeIndiceUnico("ux_perfis_nome")));

        Result<EnvioDoDocumentoDto> resultado = await cenario.Versoes.CriarPerfilAsync(Dados("Analista"), CancellationToken.None);

        resultado.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.NomeDuplicado);
        cenario.Unidade.Descartes.Should().Be(1);
    }

    private static DadosDaVersaoRequest Dados(string nome, params string[] permissoes) =>
        new(nome, Descricao, permissoes.Length == 0 ? [Consultar] : permissoes, PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento);

    private sealed class ViolacaoDeIndiceUnico(string constraintName) : Exception
    {
        public string SqlState { get; } = "23505";

        public string ConstraintName { get; } = constraintName;
    }

    private sealed class Cenario
    {
        public Cenario(params Perfil[] perfis)
            : this(PerfisDeTeste.Autor, perfis)
        {
        }

        public Cenario(bool janelaAberta, params Perfil[] perfis)
            : this(PerfisDeTeste.Autor, perfis, janelaAberta)
        {
        }

        public Cenario(Exception falha)
            : this(PerfisDeTeste.Autor, [], false, falha)
        {
        }

        public Cenario(Perfil perfil, Exception falha)
            : this(PerfisDeTeste.Autor, [perfil], false, falha)
        {
        }

        public Cenario(Identidade? autor, params Perfil[] perfis)
            : this(autor, perfis, false)
        {
        }

        private Cenario(Identidade? autor, Perfil[] perfis, bool janelaAberta, Exception? falha = null)
        {
            Perfis = new PerfisNaMemoria(perfis);
            Unidade = new UnidadeDeTrabalho { Falha = falha };
            Armazenamento = new Armazenamento();
            Versoes = new VersoesDoPerfil(
                new OperacaoSobreOPerfil(Perfis, Unidade, new CenarioDaGovernancaDaVersao.RelogioFixo(PerfisDeTeste.Instante.AddHours(1))),
                new LeitorDoContextoDaVersao(Perfis, new JanelasNaMemoria(janelaAberta), TimeProvider.System),
                new EnvioDoDocumento(Armazenamento, TimeProvider.System),
                new AutoriaDaOperacao(new Usuario(autor), TimeProvider.System, new ConcessoesNaMemoria()),
                Catalogo);
        }

        public PerfisNaMemoria Perfis { get; }

        public UnidadeDeTrabalho Unidade { get; }

        public Armazenamento Armazenamento { get; }

        public VersoesDoPerfil Versoes { get; }
    }
}
