namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os identificadores de banco que o modelo relacional de um <see cref="IModel"/> cria: tabela, colunas, chaves, chaves
/// estrangeiras, índices e restrições de verificação. Os nomes só são os que o EF derivaria sem o limite do banco quando o modelo
/// vem de <see cref="ModelosSemTruncamento"/>; num modelo comum, o EF já devolve cortado o nome que ele mesmo gera.
/// </summary>
internal static class IdentificadoresDoModelo
{
    public const string Tabela = "tabela";
    public const string Coluna = "coluna";
    public const string Chave = "chave primária ou de unicidade";
    public const string ChaveEstrangeira = "chave estrangeira";
    public const string Indice = "índice";
    public const string RestricaoDeVerificacao = "restrição de verificação";

    internal static IEnumerable<IdentificadorDoBanco> Do(IModel modelo) =>
        modelo.GetRelationalModel().Tables.SelectMany(Do).Distinct();

    private static IEnumerable<IdentificadorDoBanco> Do(ITable tabela) =>
        NomesPorTipo(tabela).SelectMany(grupo => grupo.Nomes.OfType<string>()
            .Select(nome => new IdentificadorDoBanco(tabela.Schema ?? string.Empty, tabela.Name, grupo.Tipo, nome)));

    private static (string Tipo, IEnumerable<string?> Nomes)[] NomesPorTipo(ITable tabela)
    {
        StoreObjectIdentifier objeto = StoreObjectIdentifier.Table(tabela.Name, tabela.Schema);
        return
        [
            (Tabela, [tabela.Name]),
            (Coluna, tabela.Columns.SelectMany(c => c.PropertyMappings).Select(m => m.Property.GetColumnName(objeto))),
            (Chave, tabela.UniqueConstraints.SelectMany(c => c.MappedKeys).Select(k => k.GetName(objeto))),
            (ChaveEstrangeira, tabela.ForeignKeyConstraints.SelectMany(c => NomesDaChaveEstrangeira(c, objeto))),
            (Indice, tabela.Indexes.SelectMany(i => i.MappedIndexes).Select(i => i.GetDatabaseName(objeto))),
            (RestricaoDeVerificacao, tabela.EntityTypeMappings.Select(m => m.TypeBase).OfType<IEntityType>()
                .SelectMany(e => e.GetCheckConstraints()).Select(c => c.Name)),
        ];
    }

    private static IEnumerable<string?> NomesDaChaveEstrangeira(IForeignKeyConstraint restricao, StoreObjectIdentifier objeto)
    {
        StoreObjectIdentifier principal = StoreObjectIdentifier.Table(restricao.PrincipalTable.Name, restricao.PrincipalTable.Schema);
        return restricao.MappedForeignKeys.Select(chave => chave.GetConstraintName(objeto, principal));
    }
}
