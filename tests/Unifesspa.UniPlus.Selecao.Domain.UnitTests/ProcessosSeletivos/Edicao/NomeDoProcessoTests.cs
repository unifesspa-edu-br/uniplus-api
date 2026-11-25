namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class NomeDoProcessoTests
{
    [Theory]
    [InlineData(300)]
    [InlineData(1)]
    public void Criar_NomeDentroDoLimite_Aceita(int comprimento)
    {
        ProcessoSeletivo processo = Criar(new string('a', comprimento));
        processo.Nome.Should().HaveLength(comprimento);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Criar_NomeVazio_Recusa(string nome)
    {
        Action criar = () => Criar(nome);
        criar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Criar_NomeComMaisDeTrezentosCaracteres_Recusa()
    {
        Action criar = () => Criar(new string('a', 301));
        criar.Should().Throw<ArgumentException>();
    }

    private static ProcessoSeletivo Criar(string nome) => ProcessoSeletivo.Criar(
        nome, TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de seleção", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
}
