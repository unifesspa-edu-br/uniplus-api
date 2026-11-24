namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Persistence;

using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Monolito.Persistencia;

internal sealed class NomesDoBanco
{
    private readonly IEntityType[] _entidades;

    internal NomesDoBanco(string esquema)
    {
        using MonolitoDbContext contexto = new(UniPlusDbContextOptionsExtensions.BuildDesignTimeOptions<MonolitoDbContext>());
        _entidades = contexto.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Where(tipo => tipo.GetSchema() == esquema && tipo.GetTableName() is not null).ToArray();
        _entidades.Should().NotBeEmpty("o esquema do módulo deve ter tabelas no modelo");
    }

    internal string[] IndicesComPrefixoInvalido() => Indices()
        .Where(item => !item.Nome.StartsWith($"{(item.Unico ? "ux" : "ix")}_{item.Tabela}_", StringComparison.Ordinal))
        .Select(item => item.Nome).Distinct().Order().ToArray();

    internal string[] RestricoesComPrefixoInvalido() => Restricoes()
        .Where(item => !item.Nome.StartsWith($"{item.Prefixo}_{item.Tabela}_", StringComparison.Ordinal))
        .Select(item => item.Nome).Distinct().Order().ToArray();

    internal string[] NomesTerminadosEmVivo() => TodosOsNomes()
        .Where(nome => nome.EndsWith("_vivo", StringComparison.Ordinal)).Distinct().Order().ToArray();

    internal string[] NomesLongos() => TodosOsNomes()
        .Where(nome => Encoding.UTF8.GetByteCount(nome) > 63).Distinct().Order().ToArray();

    internal string[] TabelasLongas() => _entidades.Select(tipo => tipo.GetTableName()!)
        .Where(nome => nome.Length > 30).Distinct().Order().ToArray();

    private IEnumerable<(string Tabela, string Nome, bool Unico)> Indices() =>
        _entidades.SelectMany(tipo => tipo.GetIndexes()
            .Select(indice => (Tabela: tipo.GetTableName()!, Nome: indice.GetDatabaseName(), Unico: indice.IsUnique)))
            .Where(item => item.Nome is not null)
            .Select(item => (item.Tabela, item.Nome!, item.Unico));

    private IEnumerable<(string Tabela, string Nome, string Prefixo)> Restricoes() =>
        _entidades.SelectMany(tipo => Verificacoes(tipo).Concat(ChavesEstrangeiras(tipo)));

    private static IEnumerable<(string Tabela, string Nome, string Prefixo)> Verificacoes(IEntityType tipo) =>
        tipo.GetCheckConstraints().Select(check => check.Name)
            .OfType<string>()
            .Select(nome => (tipo.GetTableName()!, nome, "ck"));

    private static IEnumerable<(string Tabela, string Nome, string Prefixo)> ChavesEstrangeiras(IEntityType tipo) =>
        tipo.GetForeignKeys().Select(chave => chave.GetConstraintName())
            .Where(nome => nome is not null)
            .Select(nome => (tipo.GetTableName()!, nome!, "fk"));

    private IEnumerable<string> TodosOsNomes() => Indices().Select(item => item.Nome)
        .Concat(Restricoes().Select(item => item.Nome)).Concat(ChavesPrimariasExplicitas());

    private IEnumerable<string> ChavesPrimariasExplicitas() => _entidades
        .Select(tipo => tipo.FindPrimaryKey()?.FindAnnotation("Relational:Name")?.Value).OfType<string>();
}
