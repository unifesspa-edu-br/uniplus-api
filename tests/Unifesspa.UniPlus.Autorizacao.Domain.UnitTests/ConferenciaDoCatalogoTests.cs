namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// A comparação do catálogo com a base de cada concessão e com a governança: a base acompanha só a descida, o escopo
/// mudado guarda o escopo anterior, e o perfil de governança nunca se suspende, só gera divergência.
/// </summary>
public sealed class ConferenciaDoCatalogoTests
{
    private const string AprovacaoDaProtecaoDoCatalogo = "configuracao:protecao-do-catalogo:aprovar";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;
    private static readonly Guid Catalogo = Guid.CreateVersion7();

    /// <summary>
    /// As permissões de decisão protegida: só o perfil de Representante do controlador as reúne. A aprovação da proteção do
    /// catálogo é do módulo de configuração, reservada à governança.
    /// </summary>
    private static readonly PermissaoAutorizacao[] DecisoesProtegidas =
    [
        PermissaoAutorizacao.PerfisAprovarProtecaoDeDados,
        PermissaoAutorizacao.PedidosAprovarProtecaoDeDados,
        PermissaoAutorizacao.RevisoesConduzir,
    ];

    private static CodigoDePermissao[] CodigosDasDecisoesProtegidas =>
        [.. DecisoesProtegidas.Select(static p => CodigoDePermissao.De(CatalogoDePermissoes<PermissaoAutorizacao>.Obter(p).Codigo)), CodigoDePermissao.De(AprovacaoDaProtecaoDoCatalogo)];

    private static CodigoDePermissao PublicacaoDoTermo => CodigoDePermissao.De(CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.TermosPublicar).Codigo);

    private static PermissaoDoCatalogo Linha(string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade = false) =>
        new(CodigoDePermissao.De(codigo), classificacao, deUnidade, false);

    [Fact(DisplayName = "A base sem diferença de classe nem de escopo não gera mudança")]
    public void Comparacao_SemDiferenca_NaoMuda() =>
        ComparacaoComOCatalogo.MudancaDe(new BaseDaConcessao(ClassificacaoDeProtecao.Interno, false), Linha("a:b:c", ClassificacaoDeProtecao.Interno))
            .Should().BeNull();

    [Theory(DisplayName = "A mudança de classe ou de escopo é uma mudança, e cada eixo vale por conta própria")]
    [InlineData(ClassificacaoDeProtecao.Sensivel, false)]
    [InlineData(ClassificacaoDeProtecao.Publico, false)]
    [InlineData(ClassificacaoDeProtecao.Interno, true)]
    public void Comparacao_ComDiferenca_Muda(ClassificacaoDeProtecao nova, bool deUnidade)
    {
        if (ComparacaoComOCatalogo.MudancaDe(new BaseDaConcessao(ClassificacaoDeProtecao.Interno, false), Linha("a:b:c", nova, deUnidade)) is not { } mudanca)
        {
            Assert.Fail("A diferença de classe ou de escopo devia ter gerado uma mudança.");
            return;
        }

        mudanca.ClassificacaoNova.Should().Be(nova);
        mudanca.DeUnidadeNovo.Should().Be(deUnidade);
    }

    [Fact(DisplayName = "A base da conciliação desce com a classe do catálogo e nunca sobe com ela")]
    public void BaseDaConciliacao_AcompanhaSoADescida()
    {
        BaseDaConcessao atual = new(ClassificacaoDeProtecao.Pessoal, false);
        MudancaDaPermissao descida = new(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Interno, false, false);
        MudancaDaPermissao subida = new(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Sensivel, false, false);

        ComparacaoComOCatalogo.BaseDaConciliacao(atual, descida).Classificacao.Should().Be(ClassificacaoDeProtecao.Interno);
        ComparacaoComOCatalogo.BaseDaConciliacao(atual, subida).Classificacao.Should().Be(ClassificacaoDeProtecao.Pessoal);
    }

    [Fact(DisplayName = "Na mudança de escopo a base guarda o escopo anterior do catálogo, para continuar suspensa")]
    public void BaseDaConciliacao_GuardaOEscopoAnterior()
    {
        BaseDaConcessao atual = new(ClassificacaoDeProtecao.Interno, false);
        MudancaDaPermissao escopo = new(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Interno, false, true);

        BaseDaConcessao baseNova = ComparacaoComOCatalogo.BaseDaConciliacao(atual, escopo);

        baseNova.DeUnidade.Should().BeFalse();
        baseNova.SuspendeFrenteA(ClassificacaoDeProtecao.Interno, true).Should().BeTrue();
    }

    [Fact(DisplayName = "A composição gravada igual à esperada não gera divergência")]
    public void Governanca_ComposicaoIgual_SemDivergencia() =>
        ConferenciaDaGovernanca.DaComposicao(Catalogo, ComposicaoDaGovernanca.Esperada, Agora).Should().BeEmpty();

    [Fact(DisplayName = "A permissão que falta e a que sobra no perfil de governança viram divergência de composição")]
    public void Governanca_ComposicaoDivergente_GeraDivergencias()
    {
        PermissaoDeGovernanca faltante = ComposicaoDaGovernanca.Esperada[0];
        PermissaoDeGovernanca excedente = new(PerfisDeSistema.GestorDeAcessosId, CodigoDePermissao.De("selecao:processos:configurar"));

        IReadOnlyList<DivergenciaDaGovernanca> divergencias = ConferenciaDaGovernanca.DaComposicao(
            Catalogo, [.. ComposicaoDaGovernanca.Esperada.Skip(1), excedente], Agora);

        divergencias.Should().HaveCount(2);
        divergencias.Should().ContainSingle(d => d.Tipo == TipoDeDivergencia.ComposicaoFaltante && d.Codigo == faltante.Codigo.Valor);
        divergencias.Should().ContainSingle(d => d.Tipo == TipoDeDivergencia.ComposicaoExcedente && d.Codigo == excedente.Codigo.Valor);
    }

    [Fact(DisplayName = "A permissão de governança reclassificada ou de escopo mudado vira divergência com o valor antes e depois")]
    public void Governanca_PermissaoReclassificada_GeraDivergencia()
    {
        CodigoDePermissao codigo = ComposicaoDaGovernanca.Esperada[0].Codigo;
        Dictionary<CodigoDePermissao, PermissaoDoCatalogo> anterior = new() { [codigo] = Linha(codigo.Valor, ClassificacaoDeProtecao.Interno) };
        Dictionary<CodigoDePermissao, PermissaoDoCatalogo> novo = new() { [codigo] = Linha(codigo.Valor, ClassificacaoDeProtecao.Sensivel, deUnidade: true) };

        IReadOnlyList<DivergenciaDaGovernanca> divergencias = ConferenciaDaGovernanca.DoCatalogo(Catalogo, anterior, novo, Agora);

        divergencias.Should().Contain(d => d.Tipo == TipoDeDivergencia.Classificacao && d.De == "INTERNO" && d.Para == "SENSIVEL");
        divergencias.Should().Contain(d => d.Tipo == TipoDeDivergencia.Escopo && d.De == "False" && d.Para == "True");
    }

    [Fact(DisplayName = "A permissão de governança sem mudança no catálogo não gera divergência")]
    public void Governanca_PermissaoIgual_SemDivergencia()
    {
        CodigoDePermissao codigo = ComposicaoDaGovernanca.Esperada[0].Codigo;
        Dictionary<CodigoDePermissao, PermissaoDoCatalogo> catalogo = new() { [codigo] = Linha(codigo.Valor, ClassificacaoDeProtecao.Interno) };

        ConferenciaDaGovernanca.DoCatalogo(Catalogo, catalogo, catalogo, Agora).Should().BeEmpty();
    }

    [Fact(DisplayName = "A composição esperada de cada perfil de governança lista só os códigos dele")]
    public void Composicao_CodigosPorPerfil()
    {
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeAcessosId).Should().HaveCount(7);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.RepresentanteDoControladorId).Should().HaveCount(6);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeProtecaoDeDadosId).Should().HaveCount(8);
        ComposicaoDaGovernanca.CodigosDe(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact(DisplayName = "As permissões de decisão protegida estão no perfil de Representante do controlador e não no do gestor nem no do Gestor de proteção de dados")]
    public void Composicao_DecisoesProtegidas_SoNoRepresentante()
    {
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.RepresentanteDoControladorId).Should().Contain(CodigosDasDecisoesProtegidas);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeAcessosId).Should().NotContain(CodigosDasDecisoesProtegidas);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeProtecaoDeDadosId).Should().NotContain(CodigosDasDecisoesProtegidas);
    }

    [Fact(DisplayName = "O Gestor de proteção de dados não reúne decisão, revogação nem administração de concessões")]
    public void Composicao_GestorDeProtecaoDeDados_SoConsultaManifestaESolicita()
    {
        IReadOnlyList<CodigoDePermissao> codigos = ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeProtecaoDeDadosId);

        codigos.Should().NotContain(c => c.Acao.StartsWith("aprovar", StringComparison.Ordinal) || c.Acao == "conduzir");
        codigos.Select(static c => c.Valor).Should().NotContain(["autorizacao:concessoes:administrar", "autorizacao:perfis:manter", "autorizacao:revisoes:rever-protecao-de-dados"]);
        codigos.Select(static c => c.Valor).Should().Contain(["autorizacao:permissoes:consultar-catalogo", "configuracao:protecao-do-catalogo:manifestar", "autorizacao:providencias:solicitar"]);
    }

    [Theory(DisplayName = "A governança admite qualquer permissão da autorização e, de outro módulo, só a reservada")]
    [InlineData("autorizacao", false, true)]
    [InlineData("autorizacao", true, true)]
    [InlineData("configuracao", true, true)]
    [InlineData("configuracao", false, false)]
    [InlineData("selecao", false, false)]
    public void Composicao_Admite_ReservadaDeOutroModulo(string modulo, bool reservada, bool admite) =>
        ComposicaoDaGovernanca.Admite(new PermissaoDoCatalogo(CodigoDePermissao.De(modulo + ":a:b"), ClassificacaoDeProtecao.Pessoal, false, reservada))
            .Should().Be(admite);

    [Fact(DisplayName = "A publicação do termo de responsabilidade é só do gestor de acessos")]
    public void Composicao_PublicacaoDoTermo_SoNoGestor()
    {
        ComposicaoDaGovernanca.Esperada.Where(p => p.Codigo == PublicacaoDoTermo)
            .Should().ContainSingle().Which.PerfilId.Should().Be(PerfisDeSistema.GestorDeAcessosId);
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.TermosPublicar).ReservadaAGovernanca.Should().BeTrue();
    }

    [Fact(DisplayName = "Toda aprovação de proteção de dados da composição é decisão protegida e só um perfil a reúne")]
    public void Composicao_AprovacoesDeProtecao_SaoDecisoesProtegidasExclusivas()
    {
        string sufixo = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo.Split(':')[^1];

        ComposicaoDaGovernanca.Esperada.Where(p => p.Codigo.Acao == sufixo)
            .Select(static p => p.Codigo).Should().OnlyContain(c => CodigosDasDecisoesProtegidas.Contains(c));
        ComposicaoDaGovernanca.Esperada.Where(p => CodigosDasDecisoesProtegidas.Contains(p.Codigo))
            .Should().OnlyContain(static p => p.PerfilId == PerfisDeSistema.RepresentanteDoControladorId);
    }
}
