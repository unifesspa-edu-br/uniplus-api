namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.TestesCompartilhados;

using Xunit;

public sealed class LimitesDoEnvelopeBatemComOSchemaTests
{
    public static TheoryData<string, int> Comprimentos => Dados(ClasseDeConfrontoDoLimite.Comprimento);
    public static TheoryData<string, int> Precisoes => Dados(ClasseDeConfrontoDoLimite.Precisao);

    [Theory]
    [MemberData(nameof(Comprimentos))]
    public void Comprimento_BateComAsColunas(string nome, int valor)
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        (string Nome, Type Entidade, string Propriedade)[] colunas = ColunasDosLimitesDoEnvelope.Comprimentos.Where(coluna => coluna.Nome == nome).ToArray();
        colunas.Should().NotBeEmpty($"{nome} precisa indicar as colunas confrontadas");
        foreach ((string Nome, Type Entidade, string Propriedade) coluna in colunas)
        {
            Coluna(contexto, coluna.Entidade, coluna.Propriedade).GetMaxLength().Should().Be(valor, nome);
        }
    }

    [Theory]
    [MemberData(nameof(Precisoes))]
    public void Precisao_BateComAsColunas(string nome, int valor)
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        (string Nome, int Escala, Type Entidade, string Propriedade)[] colunas = ColunasDosLimitesDoEnvelope.Precisoes.Where(coluna => coluna.Nome == nome).ToArray();
        colunas.Should().NotBeEmpty($"{nome} precisa indicar as colunas confrontadas");
        foreach ((string Nome, int Escala, Type Entidade, string Propriedade) coluna in colunas)
        {
            IProperty propriedade = Coluna(contexto, coluna.Entidade, coluna.Propriedade);
            propriedade.GetPrecision().Should().Be(valor, nome);
            propriedade.GetScale().Should().Be(coluna.Escala, nome);
        }
    }

    private static TheoryData<string, int> Dados(ClasseDeConfrontoDoLimite classe)
    {
        TheoryData<string, int> dados = [];
        foreach (EntradaDeConfrontoDoLimite entrada in QuadroDeConfrontoDosLimites.Entradas.Where(entrada => entrada.Classe == classe))
        {
            dados.Add(entrada.Nome, entrada.Valor);
        }
        return dados;
    }

    private static IProperty Coluna(UniPlusDbContext contexto, Type entidade, string caminho)
    {
        IEntityType tipo = contexto.Model.FindEntityType(entidade)!;
        string[] partes = caminho.Split('.');
        foreach (string navegacao in partes[..^1])
        {
            tipo = tipo.FindNavigation(navegacao)!.TargetEntityType;
        }
        return tipo.FindProperty(partes[^1])!;
    }
}
