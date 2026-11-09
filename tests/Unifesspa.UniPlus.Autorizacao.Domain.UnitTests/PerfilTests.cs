namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PerfilTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Manter = "autorizacao:perfis:manter";
    private const string Descricao = "Descrição válida.";

    private static readonly FakeCatalogo Catalogo = new(
        existentes: [Consultar, "selecao:processos:publicar", Manter, "unidade:inscricoes:operar"],
        reservadas: [Manter]);

    [Fact(DisplayName = "Perfil comum nasce ativo, sem governança, com a versão 1 pendente e as permissões consolidadas")]
    public void Criar_Valido_NasceComVersaoPendente()
    {
        Perfil perfil = PerfisDeTeste.Pendente(
            "  Analista de inscrições  ", "Acompanha as inscrições do processo.", [Consultar, Consultar], Catalogo);

        perfil.Ativo.Should().BeTrue();
        perfil.Governanca.Should().BeNull();
        perfil.Nome.Should().Be("Analista de inscrições");
        perfil.Versoes.Should().ContainSingle().Which.Numero.Should().Be(1);
        perfil.VersaoPendente!.Codigos().Should().Equal(Consultar);
        perfil.VersaoPendente.Documento!.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
        perfil.VersaoVigente.Should().BeNull();
        perfil.Vigencias.Should().BeEmpty();
    }

    [Fact(DisplayName = "Perfil comum sem versão vigente não tem composição em vigor")]
    public void PerfilSemVigente_NaoTemComposicaoEmVigor()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        perfil.TemComposicaoEmVigor.Should().BeFalse();
        perfil.ComposicaoEmVigor.Should().BeEmpty();
        perfil.Permissoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Perfil de governança tem composição em vigor sem versão")]
    public void PerfilDeGovernanca_TemComposicaoSemVersao()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], Catalogo);

        perfil.TemComposicaoEmVigor.Should().BeTrue();
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Fact(DisplayName = "Lista de permissões vazia é recusada")]
    public void Validar_SemPermissao_Recusa() =>
        CodigosDaRecusa(Validar("Analista", Descricao, []))
            .Should().Equal(PerfilErrorCodes.SemPermissao);

    [Fact(DisplayName = "Código fora do catálogo é recusado e a recusa lista o código")]
    public void Validar_PermissaoInexistente_RecusaListandoOCodigo()
    {
        Result<ConteudoDaVersao> resultado = Validar("Analista", Descricao, [Consultar, "x:y:z"]);

        CodigosDaRecusa(resultado).Should().Equal(PerfilErrorCodes.PermissaoInexistente);
        resultado.Errors.Single().Error.Message.Should().Contain("x:y:z").And.NotContain(Consultar);
    }

    [Fact(DisplayName = "Perfil comum não reúne permissão reservada à governança")]
    public void Validar_PermissaoReservada_Recusa()
    {
        Result<ConteudoDaVersao> resultado = Validar("Analista", Descricao, [Consultar, Manter]);

        CodigosDaRecusa(resultado).Should().Equal(PerfilErrorCodes.PermissaoReservada);
        resultado.Errors.Single().Error.Message.Should().Contain(Manter);
    }

    [Fact(DisplayName = "Nome, descrição e protocolo inválidos se acumulam com as recusas de permissão e de tamanho")]
    public void Validar_VariasViolacoes_AcumulaTodas() =>
        CodigosDaRecusa(ConteudoDaVersao.Validar(" ", "ab", [], " ", 0, Catalogo))
            .Should().BeEquivalentTo(
                PerfilErrorCodes.NomeInvalido,
                PerfilErrorCodes.DescricaoInvalida,
                PerfilErrorCodes.ProtocoloInvalido,
                PerfilErrorCodes.SemPermissao,
                PerfilErrorCodes.DocumentoInvalido);

    [Fact(DisplayName = "Nome com caractere nulo é recusado pelo domínio")]
    public void Validar_NomeComCaractereNulo_Recusa() =>
        CodigosDaRecusa(Validar("Ana\0lista", Descricao, [Consultar]))
            .Should().Equal(PerfilErrorCodes.NomeInvalido);

    [Theory(DisplayName = "Tamanho do documento fora de 1 byte a 10 MB é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(DocumentoDaVersao.TamanhoMaximoBytes + 1)]
    public void Validar_TamanhoForaDoLimite_Recusa(long tamanho) =>
        CodigosDaRecusa(ConteudoDaVersao.Validar("Analista", Descricao, [Consultar], PerfisDeTeste.Protocolo, tamanho, Catalogo))
            .Should().Equal(PerfilErrorCodes.DocumentoInvalido);

    [Theory(DisplayName = "Tamanho do documento nos limites é aceito")]
    [InlineData(1)]
    [InlineData(DocumentoDaVersao.TamanhoMaximoBytes)]
    public void Validar_TamanhoNosLimites_Aceita(long tamanho) =>
        ConteudoDaVersao.Validar("Analista", Descricao, [Consultar], PerfisDeTeste.Protocolo, tamanho, Catalogo)
            .IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Perfil que mistura permissão de unidade e institucional é recusado")]
    public void AlcanceMisto_Recusa() =>
        CodigosDaRecusa(Validar("Analista", Descricao, [Consultar, "unidade:inscricoes:operar"]))
            .Should().Equal(PerfilErrorCodes.AlcanceMisto);

    [Fact(DisplayName = "A versão guarda o retrato da classificação e do alcance de cada permissão")]
    public void Versao_GuardaORetratoDasPermissoes()
    {
        FakeCatalogo catalogo = new(["unidade:inscricoes:operar"], [], sensiveis: ["unidade:inscricoes:operar"]);

        Perfil perfil = PerfisDeTeste.Pendente("Operador", Descricao, ["unidade:inscricoes:operar"], catalogo);

        PermissaoDaVersao permissao = perfil.VersaoPendente!.Permissoes.Single();
        permissao.Classificacao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        permissao.DeUnidade.Should().BeTrue();
    }

    [Fact(DisplayName = "O alcance do perfil é o das permissões em vigor")]
    public void Alcance_DerivaDoCatalogo()
    {
        PerfisDeTeste.Vigente("Analista", Descricao, [Consultar], Catalogo)
            .Alcance(Catalogo).Should().Be(TipoDeEscopo.Instituicao);
        PerfisDeTeste.Vigente("Operador", Descricao, ["unidade:inscricoes:operar"], Catalogo)
            .Alcance(Catalogo).Should().Be(TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "Permissão retirada do catálogo não muda o alcance das demais")]
    public void Alcance_IgnoraPermissaoRetiradaDoCatalogo()
    {
        FakeCatalogo antes = new(["unidade:inscricoes:operar", "unidade:laudos:consultar"], []);
        FakeCatalogo depois = new(["unidade:inscricoes:operar"], []);
        Perfil perfil = PerfisDeTeste.Vigente("Operador", Descricao, ["unidade:inscricoes:operar", "unidade:laudos:consultar"], antes);

        perfil.Alcance(depois).Should().Be(TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "Perfil de governança recusa criar versão, reativação e desativação")]
    public void Governanca_RecusaAlteracao()
    {
        Perfil perfil = PerfisDeTeste.DeGovernanca("Gestor de acessos", Descricao, [Consultar], Catalogo);

        CodigosDaRecusa(perfil.CriarVersao(
            PerfisDeTeste.Conteudo("Outro", Descricao, [Consultar], Catalogo),
            PerfisDeTeste.SemContexto,
            PerfisDeTeste.Autor,
            PerfisDeTeste.Instante,
            Catalogo)).Should().Equal(PerfilErrorCodes.GovernancaImutavel);
        perfil.Desativar().Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Reativar().Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Desativar e reativar perfil comum alterna o estado e preserva a versão pendente")]
    public void DesativarEReativar_PerfilComum_AlternaOEstado()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Analista", Descricao, [Consultar], Catalogo);

        perfil.Desativar().IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeFalse();
        perfil.VersaoPendente.Should().NotBeNull();
        perfil.Reativar().IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
    }

    private static Result<ConteudoDaVersao> Validar(string nome, string descricao, string[] permissoes) =>
        ConteudoDaVersao.Validar(nome, descricao, permissoes, PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento, Catalogo);

    private static string[] CodigosDaRecusa(Result resultado) =>
        [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDaRecusa<T>(Result<T> resultado) =>
        [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class FakeCatalogo(string[] existentes, string[] reservadas, string[]? sensiveis = null) : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => existentes.Contains(codigo);

        public bool ReservadaAGovernanca(string codigo) => reservadas.Contains(codigo);

        public bool DeUnidade(string codigo) => Existe(codigo) && codigo.StartsWith("unidade:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) =>
            sensiveis?.Contains(codigo) == true ? ClassificacaoDeProtecao.Sensivel : ClassificacaoDeProtecao.Interno;
    }
}
