namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed class ChaveDasBancasTests
{
    private static readonly ChaveLocal Chave = ChaveLocal.Criar("banca_original").Value;
    private static readonly ChaveLocal OutraChave = ChaveLocal.Criar("banca_nova").Value;
    private static readonly TipoDeBancaCopiado Tipo = TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista);

    [Fact]
    public void Criar_BancasDaFaseEDaEtapa_GuardaChaveRecebida()
    {
        BancaRequerida.Criar(Chave, Tipo, []).Chave.Should().Be(Chave);
        BancaDaEtapa.Criar(Chave, Tipo).Chave.Should().Be(Chave);
    }

    [Fact]
    public void ReconstruirDosDadosSalvos_BancasDaFaseEDaEtapa_MantemChaveRecebida()
    {
        BancaRequerida.ReconstruirDosDadosSalvos(Chave, Guid.CreateVersion7(), Tipo, []).Chave.Should().Be(Chave);
        BancaDaEtapa.ReconstruirDosDadosSalvos(Chave, Guid.CreateVersion7(), Tipo).Chave.Should().Be(Chave);
    }

    [Fact]
    public void AssumirDeclaracao_BancaDaFase_MantemChaveOriginal()
    {
        BancaRequerida original = BancaRequerida.Criar(Chave, Tipo, []);
        BancaRequerida declarada = BancaRequerida.Criar(OutraChave, Tipo, [], idDaBancaQueSubstitui: original.Id);

        ReconciliacaoDasBancas.Reconciliar([original], [declarada]);

        original.Chave.Should().Be(Chave);
    }

    [Fact]
    public void AssumirDeclaracao_BancaDaEtapa_MantemChaveOriginal()
    {
        BancaDaEtapa original = BancaDaEtapa.Criar(Chave, Tipo);
        BancaDaEtapa declarada = BancaDaEtapa.Criar(OutraChave, Tipo, idDaBancaQueSubstitui: original.Id);

        ReconciliacaoDasBancas.Reconciliar([original], [declarada]);

        original.Chave.Should().Be(Chave);
    }
}
