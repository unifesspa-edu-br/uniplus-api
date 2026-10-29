namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PerfilTests
{
    private const string Consultar = "selecao:processos:consultar";
    private const string Manter = "autorizacao:perfis:manter";

    private static readonly FakeCatalogo Catalogo = new(
        existentes: [Consultar, "selecao:processos:publicar", Manter, "unidade:inscricoes:operar"],
        reservadas: [Manter]);

    [Fact(DisplayName = "Perfil comum nasce ativo, sem governança, com as permissões consolidadas")]
    public void Criar_Valido_NasceAtivoESemRepeticao()
    {
        Result<Perfil> resultado = Perfil.Criar(
            "  Analista de inscrições  ", "Acompanha as inscrições do processo.", [Consultar, Consultar], Catalogo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Ativo.Should().BeTrue();
        resultado.Value.Governanca.Should().BeNull();
        resultado.Value.Nome.Should().Be("Analista de inscrições");
        resultado.Value.Permissoes.Select(p => p.Codigo).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "Lista de permissões vazia é recusada")]
    public void Criar_SemPermissao_Recusa() =>
        CodigosDaRecusa(Perfil.Criar("Analista", "Descrição válida.", [], Catalogo))
            .Should().Equal(PerfilErrorCodes.SemPermissao);

    [Fact(DisplayName = "Código fora do catálogo é recusado e a recusa lista o código")]
    public void Criar_PermissaoInexistente_RecusaListandoOCodigo()
    {
        Result<Perfil> resultado = Perfil.Criar("Analista", "Descrição válida.", [Consultar, "x:y:z"], Catalogo);

        CodigosDaRecusa(resultado).Should().Equal(PerfilErrorCodes.PermissaoInexistente);
        resultado.Errors.Single().Error.Message.Should().Contain("x:y:z").And.NotContain(Consultar);
    }

    [Fact(DisplayName = "Perfil comum não reúne permissão reservada à governança")]
    public void Criar_PermissaoReservada_Recusa()
    {
        Result<Perfil> resultado = Perfil.Criar("Analista", "Descrição válida.", [Consultar, Manter], Catalogo);

        CodigosDaRecusa(resultado).Should().Equal(PerfilErrorCodes.PermissaoReservada);
        resultado.Errors.Single().Error.Message.Should().Contain(Manter);
    }

    [Fact(DisplayName = "Nome e descrição inválidos se acumulam com as recusas de permissão")]
    public void Criar_VariasViolacoes_AcumulaTodas() =>
        CodigosDaRecusa(Perfil.Criar(" ", "ab", [], Catalogo))
            .Should().BeEquivalentTo(
                PerfilErrorCodes.NomeInvalido, PerfilErrorCodes.DescricaoInvalida, PerfilErrorCodes.SemPermissao);

    [Fact(DisplayName = "Nome com caractere nulo é recusado pelo domínio")]
    public void Criar_NomeComCaractereNulo_Recusa() =>
        CodigosDaRecusa(Perfil.Criar("Ana\0lista", "Descrição válida.", [Consultar], Catalogo))
            .Should().Equal(PerfilErrorCodes.NomeInvalido);

    [Fact(DisplayName = "Perfil que mistura permissão de unidade e institucional é recusado na criação e na edição")]
    public void AlcanceMisto_Recusa()
    {
        CodigosDaRecusa(Perfil.Criar("Analista", "Descrição válida.", [Consultar, "unidade:inscricoes:operar"], Catalogo))
            .Should().Equal(PerfilErrorCodes.AlcanceMisto);

        Perfil perfil = Perfil.Criar("Analista", "Descrição válida.", [Consultar], Catalogo).Value!;
        CodigosDaRecusa(perfil.Atualizar("Analista", "Descrição válida.", [Consultar, "unidade:inscricoes:operar"], Catalogo))
            .Should().Equal(PerfilErrorCodes.AlcanceMisto);
    }

    [Fact(DisplayName = "O alcance do perfil é o das permissões que ele reúne")]
    public void Alcance_DerivaDoCatalogo()
    {
        Perfil.Criar("Analista", "Descrição válida.", [Consultar], Catalogo).Value!
            .Alcance(Catalogo).Should().Be(Enums.TipoDeEscopo.Instituicao);
        Perfil.Criar("Operador", "Descrição válida.", ["unidade:inscricoes:operar"], Catalogo).Value!
            .Alcance(Catalogo).Should().Be(Enums.TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "Permissão retirada do catálogo não muda o alcance das demais")]
    public void Alcance_IgnoraPermissaoRetiradaDoCatalogo()
    {
        FakeCatalogo antes = new(existentes: ["unidade:inscricoes:operar", "unidade:laudos:consultar"], reservadas: []);
        FakeCatalogo depois = new(existentes: ["unidade:inscricoes:operar"], reservadas: []);
        Perfil perfil = Perfil.Criar("Operador", "Descrição válida.", ["unidade:inscricoes:operar", "unidade:laudos:consultar"], antes).Value!;

        perfil.Alcance(depois).Should().Be(Enums.TipoDeEscopo.Unidade);
    }

    [Fact(DisplayName = "Atualizar substitui nome, descrição e permissões")]
    public void Atualizar_Valido_SubstituiOConjunto()
    {
        Perfil perfil = Perfil.Criar("Analista", "Descrição válida.", [Consultar], Catalogo).Value!;

        Result resultado = perfil.Atualizar("Publicador", "Outra descrição.", ["selecao:processos:publicar"], Catalogo);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Nome.Should().Be("Publicador");
        perfil.Permissoes.Select(p => p.Codigo).Should().Equal("selecao:processos:publicar");
    }

    [Fact(DisplayName = "Perfil de governança recusa edição, ativação e desativação")]
    public void Governanca_RecusaAlteracao()
    {
        Perfil perfil = PerfilDeGovernanca();

        CodigosDaRecusa(perfil.Atualizar("Outro", "Descrição válida.", [Consultar], Catalogo))
            .Should().Equal(PerfilErrorCodes.GovernancaImutavel);
        perfil.Desativar().Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Ativar().Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "Desativar e reativar perfil comum alterna o estado")]
    public void DesativarEReativar_PerfilComum_AlternaOEstado()
    {
        Perfil perfil = Perfil.Criar("Analista", "Descrição válida.", [Consultar], Catalogo).Value!;

        perfil.Desativar().IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeFalse();
        perfil.Ativar().IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
    }

    private static string[] CodigosDaRecusa(Result resultado) =>
        [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] CodigosDaRecusa<T>(Result<T> resultado) =>
        [.. resultado.Errors.Select(e => e.Error.Code)];

    private static Perfil PerfilDeGovernanca()
    {
        Perfil perfil = Perfil.Criar("Gestor de acessos", "Descrição válida.", [Consultar], Catalogo).Value!;
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!
            .SetValue(perfil, Domain.Enums.TipoDeGovernanca.GestorDeAcessos);
        return perfil;
    }

    private sealed class FakeCatalogo(string[] existentes, string[] reservadas) : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => existentes.Contains(codigo);

        public bool ReservadaAGovernanca(string codigo) => reservadas.Contains(codigo);

        public bool DeUnidade(string codigo) => Existe(codigo) && codigo.StartsWith("unidade:", StringComparison.Ordinal);
    }
}
