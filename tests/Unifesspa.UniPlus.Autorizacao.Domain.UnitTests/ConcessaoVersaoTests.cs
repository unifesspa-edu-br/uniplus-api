namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class ConcessaoVersaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    private static readonly DateTimeOffset Agora = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Identidade Isabella = new(Emissor, "isabella.figueiredo");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly OrigemDaConcessao Origem = new(TipoDeOrigem.Pedido, Guid.NewGuid());
    private static readonly FakeCatalogo Catalogo = new();

    [Fact(DisplayName = "A concessão do perfil comum grava a versão em vigor do perfil")]
    public void Conceder_PerfilComum_GravaAVersaoVigente()
    {
        Perfil perfil = PerfilNaVersao2();

        Concessao concessao = Conceder(perfil);

        concessao.VersaoId.Should().Be(perfil.VersaoVigente!.Id);
        perfil.VersaoVigente.Numero.Should().Be(2);
    }

    [Fact(DisplayName = "A concessão do perfil de governança não tem versão")]
    public void Conceder_Governanca_NaoTemVersao() => ConcessaoDeGovernanca().VersaoId.Should().BeNull();

    [Fact(DisplayName = "Migrar troca só a versão e mantém pessoa, escopo, validade e origem")]
    public void MigrarParaVersao_TrocaSoAVersao()
    {
        (Perfil perfil, Concessao concessao, Guid versao1) = ConcessaoNaVersao1(Agora.AddDays(30));
        Guid versao2 = perfil.VersaoVigente!.Id;

        concessao.MigrarParaVersao(perfil.Versoes.First(v => v.Id == versao1), perfil.VersaoVigente);

        concessao.VersaoId.Should().Be(versao2);
        concessao.BeneficiarioSub.Should().Be(Isabella.Sub);
        concessao.ValidaAte.Should().Be(Agora.AddDays(30));
        concessao.OrigemId.Should().Be(Origem.Id);
        concessao.UnidadeId.Should().NotBeNull();
        concessao.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "Migrar recusa a concessão revogada")]
    public void MigrarParaVersao_Revogada_Recusa()
    {
        (Perfil perfil, Concessao concessao, Guid versao1) = ConcessaoNaVersao1();
        concessao.Revogar(Diogo, "Fim da função.", Agora).IsSuccess.Should().BeTrue();

        FluentActions.Invoking(() => concessao.MigrarParaVersao(perfil.Versoes.First(v => v.Id == versao1), perfil.VersaoVigente!))
            .Should().Throw<InvalidOperationException>();
        concessao.VersaoId.Should().Be(versao1);
    }

    [Fact(DisplayName = "Migrar recusa a concessão de governança, que não tem versão")]
    public void MigrarParaVersao_Governanca_Recusa()
    {
        Concessao concessao = ConcessaoDeGovernanca();

        FluentActions.Invoking(() => concessao.MigrarParaVersao(PerfilNaVersao2().VersaoVigente!, PerfilNaVersao2().VersaoVigente!))
            .Should().Throw<InvalidOperationException>();
        concessao.VersaoId.Should().BeNull();
    }

    [Fact(DisplayName = "Migrar recusa a concessão que não está na versão esperada")]
    public void MigrarParaVersao_ForaDaVersaoEsperada_Recusa()
    {
        (Perfil perfil, Concessao concessao, Guid versao1) = ConcessaoNaVersao1();

        FluentActions.Invoking(() => concessao.MigrarParaVersao(PerfilNaVersao1().VersaoVigente!, perfil.VersaoVigente!))
            .Should().Throw<InvalidOperationException>();
        concessao.VersaoId.Should().Be(versao1);
    }

    private static Perfil PerfilNaVersao1() =>
        PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", ["selecao:processos:consultar"], Catalogo);

    private static Perfil PerfilNaVersao2()
    {
        Perfil perfil = PerfilNaVersao1();
        PerfisDeTeste.NovaVersaoVigente(perfil, ["selecao:processos:consultar", "selecao:processos:analisar"], Catalogo);

        return perfil;
    }

    private static (Perfil Perfil, Concessao Concessao, Guid Versao1) ConcessaoNaVersao1(DateTimeOffset? validaAte = null)
    {
        Perfil perfil = PerfilNaVersao1();
        Concessao concessao = Conceder(perfil, validaAte);
        Guid versao1 = perfil.VersaoVigente!.Id;
        PerfisDeTeste.NovaVersaoVigente(perfil, ["selecao:processos:consultar", "selecao:processos:analisar"], Catalogo);

        return (perfil, concessao, versao1);
    }

    private static Concessao Conceder(Perfil perfil, DateTimeOffset? validaAte = null) =>
        Concessao.Conceder(
            Isabella, perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Origem, Diogo, Agora, Catalogo, validaAte).Value!;

    private static Concessao ConcessaoDeGovernanca()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca(
            "Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], Catalogo);

        return Concessao.Conceder(
            Isabella, governanca, EscopoDaConcessao.DaInstituicao(), Origem, Diogo, Agora, Catalogo).Value!;
    }

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }
}
