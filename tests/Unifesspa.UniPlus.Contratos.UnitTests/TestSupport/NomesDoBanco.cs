namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Monolito.Persistencia;

internal sealed class NomesDoBanco
{
    private readonly ITable[] _tabelas = ModelosSemTruncamento.Modelos()
        .Single(modelo => modelo.Contexto == nameof(MonolitoDbContext)).Modelo.GetRelationalModel().Tables.ToArray();

    internal string[] Tabelas() => _tabelas.Select(tabela => tabela.Name).Distinct().Order().ToArray();

    internal string[] PrefixosInvalidos() => Nomes()
        .Where(item => item.Prefixo == "pk"
            ? item.Nome != $"pk_{item.Tabela}"
            : !item.Nome.StartsWith($"{item.Prefixo}_{item.Tabela}_", StringComparison.Ordinal))
        .Select(item => $"{item.Esquema}.{item.Tabela}: {item.Nome}").Distinct().Order().ToArray();

    internal string[] NomesTerminadosEmVivo() => Nomes().Select(item => item.Nome)
        .Where(nome => nome.EndsWith("_vivo", StringComparison.Ordinal)).Distinct().Order().ToArray();

    internal string[] NomesLongos() => Nomes().Select(item => item.Nome)
        .Where(nome => Encoding.UTF8.GetByteCount(nome) > 63).Distinct().Order().ToArray();

    private IEnumerable<(string Esquema, string Tabela, string Nome, string Prefixo)> Nomes() =>
        _tabelas.SelectMany(tabela => NomesDaTabela(tabela).Select(item =>
            (tabela.Schema ?? "", tabela.Name, item.Nome, item.Prefixo)));

    private static IEnumerable<(string Nome, string Prefixo)> NomesDaTabela(ITable tabela) =>
        tabela.Indexes.Select(indice => (indice.Name, indice.IsUnique ? "ux" : "ix"))
            .Concat(tabela.ForeignKeyConstraints.Select(chave => (chave.Name, "fk")))
            .Concat(tabela.EntityTypeMappings.Select(m => m.TypeBase).OfType<IEntityType>()
                .SelectMany(tipo => tipo.GetCheckConstraints()).Select(check => (check.Name!, "ck")))
            .Concat(tabela.PrimaryKey is null ? [] : [(tabela.PrimaryKey.Name, "pk")]);
}
