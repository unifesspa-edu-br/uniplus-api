namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegraDeNomeDeTabelaTests
{
    [Theory(DisplayName = "Nome no plural, em snake_case e com até 30 caracteres não tem motivo de recusa")]
    [InlineData("editais")]
    [InlineData("inscricoes")]
    [InlineData("processos_seletivos")]
    [InlineData("aceites_do_termo")]
    [InlineData("tipos_processo")]
    [InlineData("nos_da_arvore_de_exigencias")]
    [InlineData("acessos_ao_documento_da_versao")]
    public void Motivos_NomeConforme_NaoTemMotivo(string tabela) =>
        RegraDeNomeDeTabela.Motivos(tabela).Should().BeEmpty();

    [Theory(DisplayName = "Núcleo no singular é recusado mesmo com complemento no plural")]
    [InlineData("ato_normativo", "ato")]
    [InlineData("tipo_banca", "tipo")]
    [InlineData("instalacao_gestores", "instalacao")]
    [InlineData("edital", "edital")]
    public void Motivos_NucleoNoSingular_ApontaONucleo(string tabela, string nucleo) =>
        RegraDeNomeDeTabela.Motivos(tabela).Should().ContainSingle().Which.Should().Contain($"o núcleo '{nucleo}' não está no plural");

    [Fact(DisplayName = "Nome de 31 caracteres é recusado e o de 30 é aceito")]
    public void Motivos_LimiteDeCaracteres_RecusaAcimaDe30()
    {
        string com30 = "acessos_ao_documento_da_versao";
        string com31 = "leituras_do_documento_da_versao";

        RegraDeNomeDeTabela.Motivos(com30).Should().BeEmpty();
        RegraDeNomeDeTabela.Motivos(com31).Should().ContainSingle().Which.Should().Contain("31 caracteres");
    }

    [Theory(DisplayName = "Nome fora do snake_case é recusado")]
    [InlineData("AtosNormativos")]
    [InlineData("Editais")]
    [InlineData("atos-normativos")]
    [InlineData("atos normativos")]
    [InlineData("atos_normativos_")]
    [InlineData("atos__normativos")]
    [InlineData("1_atos")]
    [InlineData("ações")]
    public void Motivos_ForaDoSnakeCase_Recusa(string tabela) =>
        RegraDeNomeDeTabela.Motivos(tabela).Should().Contain(m => m.Contains("snake_case", StringComparison.Ordinal));

    [Fact(DisplayName = "A recusa lista esquema.tabela e o motivo, em ordem estável")]
    public void Violacoes_ListaEsquemaTabelaEMotivo()
    {
        (string, string)[] tabelas = [("selecao", "inscricoes"), ("publicacoes", "ato_normativo"), ("selecao", "edital")];

        IReadOnlyList<string> violacoes = RegraDeNomeDeTabela.Violacoes(tabelas);

        violacoes.Should().Equal(
            "publicacoes.ato_normativo: o núcleo 'ato' não está no plural",
            "selecao.edital: o núcleo 'edital' não está no plural");
    }

    [Fact(DisplayName = "Catálogo conforme não tem violação")]
    public void Violacoes_CatalogoConforme_EhVazio() =>
        RegraDeNomeDeTabela.Violacoes([("selecao", "editais"), ("configuracao", "campus")]).Should().BeEmpty();
}
