namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public sealed class FiltrosDeIndiceDoModeloDoMonolitoTests
{
    public static TheoryData<string, string, string> FiltrosEsperados() => new()
    {
        { "arquivos_da_apresentacao", "ix_arquivos_da_apresentacao_em_verificacao", "situacao = 'EM_VERIFICACAO'" },
        { "envios_de_decisoes", "ux_envios_de_decisoes_gestao", "etapa = 'GESTAO_DE_ACESSOS'" },
        { "inscricoes", "ux_inscricoes_processo_cpf_indice", "situacao <> 'CANCELADA'" },
    };

    [Theory]
    [MemberData(nameof(FiltrosEsperados))]
    public void ModeloDeProjeto_GuardaOFiltroLiteral(string tabela, string nome, string sql) =>
        FiltrosDa(ModelosDoMonolito.Valor.Projeto, tabela).Should().ContainSingle(indice => indice.Nome == nome)
            .Which.Sql.Should().Be(sql);

    [Theory]
    [MemberData(nameof(FiltrosEsperados))]
    public void InstantaneoDaMigration_GuardaOFiltroLiteral(string tabela, string nome, string sql) =>
        FiltrosDa(ModelosDoMonolito.Valor.InstantaneoDaMigration, tabela).Should().ContainSingle(indice => indice.Nome == nome)
            .Which.Sql.Should().Be(sql);

    private static List<(string? Nome, string? Sql)> FiltrosDa(IModel modelo, string tabela) =>
    [
        .. modelo.GetEntityTypes()
            .Where(entidade => entidade.GetTableName() == tabela)
            .SelectMany(entidade => entidade.GetIndexes())
            .Select(indice => (indice.GetDatabaseName(), indice.GetFilter())),
    ];
}
