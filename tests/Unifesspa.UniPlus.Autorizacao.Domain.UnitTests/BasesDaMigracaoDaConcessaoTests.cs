namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class BasesDaMigracaoDaConcessaoTests
{
    private const string Mantido = "selecao:processos:consultar";
    private const string Removido = "selecao:processos:analisar";
    private const string Novo = "selecao:processos:revisar";

    private static readonly DateTimeOffset Agora = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Isabella = new("https://idp.test/realms/unifesspa", "isabella.figueiredo");
    private static readonly Identidade Diogo = new("https://idp.test/realms/unifesspa", "diogo.souza");
    private static readonly FakeCatalogo Catalogo = new();

    [Fact(DisplayName = "Migrar grava a base do código mantido com o retrato da versão que sai")]
    public void Migrar_CodigoMantido_GravaABaseDaMigracao()
    {
        Concessao concessao = Migrada();

        BaseDaPermissaoNaConcessao baseDoMantido = concessao.Bases.Last(b => b.Codigo == Mantido);

        baseDoMantido.Origem.Should().Be(OrigemDaBase.Migracao);
        baseDoMantido.Classificacao.Should().Be(ClassificacaoDeProtecao.Interno);
        baseDoMantido.DeUnidade.Should().BeTrue();
        baseDoMantido.ReferenciaId.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Migrar encerra a base do código que sai da composição")]
    public void Migrar_CodigoQueSai_GravaASaida()
    {
        Concessao concessao = Migrada();

        BaseDaPermissaoNaConcessao baseDaSaida = concessao.Bases.Single(b => b.Codigo == Removido);

        baseDaSaida.Origem.Should().Be(OrigemDaBase.Saida);
        baseDaSaida.Classificacao.Should().BeNull();
        baseDaSaida.DeUnidade.Should().BeNull();
    }

    [Fact(DisplayName = "Migrar não grava base para o código novo da versão-alvo")]
    public void Migrar_CodigoNovo_FicaSemBase()
    {
        Concessao concessao = Migrada();

        concessao.Bases.Should().NotContain(b => b.Codigo == Novo);
    }

    private static Concessao Migrada()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", [Mantido, Removido], Catalogo);
        Concessao concessao = Concessao.Conceder(
            Isabella, perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()), Diogo, Agora, Catalogo).Value!;
        foreach (TrocaDeVigencia troca in PerfisDeTeste.Trocar(perfil, [Mantido, Novo], Catalogo))
        {
            concessao.MigrarParaVersao(troca.Anterior!, troca.Nova);
        }

        return concessao;
    }

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => true;

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}
