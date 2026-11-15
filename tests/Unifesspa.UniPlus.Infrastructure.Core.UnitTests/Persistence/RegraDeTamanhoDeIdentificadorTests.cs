namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegraDeTamanhoDeIdentificadorTests
{
    private const string Esquema = "selecao";
    private const string Tabela = "processos_seletivos";
    private const string Tipo = "índice";

    [Fact(DisplayName = "Identificador de 63 bytes é aceito e o de 64 é recusado")]
    public void Violacoes_LimiteDe63Bytes_RecusaAPartirDe64()
    {
        IdentificadorDoBanco com63 = Identificador(new string('a', 63));
        IdentificadorDoBanco com64 = Identificador(new string('b', 64));

        RegraDeTamanhoDeIdentificador.Violacoes([com63]).Should().BeEmpty();
        RegraDeTamanhoDeIdentificador.Violacoes([com64]).Should().Equal(
            $"selecao.processos_seletivos: índice '{com64.Nome}' ocupa 64 bytes e o limite é 63");
    }

    [Fact(DisplayName = "O tamanho é medido em bytes porque o PostgreSQL corta por bytes")]
    public void Violacoes_NomeComAcento_ContaBytesEmVezDeCaracteres()
    {
        string com32Caracteres = new('ç', 32);

        RegraDeTamanhoDeIdentificador.Violacoes([Identificador(com32Caracteres)]).Should().ContainSingle()
            .Which.Should().Contain("ocupa 64 bytes");
    }

    [Fact(DisplayName = "A recusa lista esquema.tabela, o tipo e o nome, em ordem estável")]
    public void Violacoes_ListaEsquemaTabelaTipoENome_EmOrdemEstavel()
    {
        string longo = new('x', 64);
        IdentificadorDoBanco[] identificadores =
        [
            new("selecao", "inscricoes", "índice", longo),
            new("publicacoes", "atos_normativos", "chave estrangeira", longo),
            new("selecao", "editais", "coluna", longo),
            new("selecao", "editais", "chave estrangeira", "fk_curta"),
        ];

        RegraDeTamanhoDeIdentificador.Violacoes(identificadores).Should().Equal(
            $"publicacoes.atos_normativos: chave estrangeira '{longo}' ocupa 64 bytes e o limite é 63",
            $"selecao.editais: coluna '{longo}' ocupa 64 bytes e o limite é 63",
            $"selecao.inscricoes: índice '{longo}' ocupa 64 bytes e o limite é 63");
    }

    [Fact(DisplayName = "Conjunto conforme não tem violação")]
    public void Violacoes_ConjuntoConforme_EhVazio() =>
        RegraDeTamanhoDeIdentificador.Violacoes([Identificador("ix_processos_seletivos_codigo"), Identificador("pk_processos_seletivos")])
            .Should().BeEmpty();

    private static IdentificadorDoBanco Identificador(string nome) => new(Esquema, Tabela, Tipo, nome);
}
