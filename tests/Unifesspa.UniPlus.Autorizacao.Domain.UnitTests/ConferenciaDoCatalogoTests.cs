namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A comparação do catálogo com a base de cada concessão e com a governança: a base acompanha só a descida, o escopo
/// mudado guarda o escopo anterior, e o perfil de governança nunca se suspende, só gera divergência.
/// </summary>
public sealed class ConferenciaDoCatalogoTests
{
    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;
    private static readonly Guid Catalogo = Guid.CreateVersion7();

    /// <summary>As permissões de decisão protegida: só o perfil de proteção de dados as reúne.</summary>
    private static readonly PermissaoAutorizacao[] DecisoesProtegidas =
    [
        PermissaoAutorizacao.PerfisAprovarProtecaoDeDados,
        PermissaoAutorizacao.PedidosAprovarProtecaoDeDados,
        PermissaoAutorizacao.RevisoesConduzir,
    ];

    private static string[] CodigosDasDecisoesProtegidas =>
        [.. DecisoesProtegidas.Select(static p => CatalogoDePermissoes<PermissaoAutorizacao>.Obter(p).Codigo)];

    private static LinhaDoRetratoDoCatalogo Linha(string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade = false) =>
        new(codigo, "autorizacao", classificacao, deUnidade, false);

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
        MudancaDaPermissao? mudanca = ComparacaoComOCatalogo.MudancaDe(
            new BaseDaConcessao(ClassificacaoDeProtecao.Interno, false), Linha("a:b:c", nova, deUnidade));

        mudanca.Should().NotBeNull();
        mudanca!.ClassificacaoNova.Should().Be(nova);
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
        PermissaoDeGovernanca excedente = new(PerfisDeSistema.GestorDeAcessosId, "selecao:processos:configurar");

        IReadOnlyList<DivergenciaDaGovernanca> divergencias = ConferenciaDaGovernanca.DaComposicao(
            Catalogo, [.. ComposicaoDaGovernanca.Esperada.Skip(1), excedente], Agora);

        divergencias.Should().HaveCount(2);
        divergencias.Should().ContainSingle(d => d.Tipo == TipoDeDivergencia.ComposicaoFaltante && d.Codigo == faltante.Codigo);
        divergencias.Should().ContainSingle(d => d.Tipo == TipoDeDivergencia.ComposicaoExcedente && d.Codigo == excedente.Codigo);
    }

    [Fact(DisplayName = "A permissão de governança reclassificada ou de escopo mudado vira divergência com o valor antes e depois")]
    public void Governanca_PermissaoReclassificada_GeraDivergencia()
    {
        string codigo = ComposicaoDaGovernanca.Esperada[0].Codigo;
        Dictionary<string, LinhaDoRetratoDoCatalogo> anterior = new() { [codigo] = Linha(codigo, ClassificacaoDeProtecao.Interno) };
        Dictionary<string, LinhaDoRetratoDoCatalogo> novo = new() { [codigo] = Linha(codigo, ClassificacaoDeProtecao.Sensivel, deUnidade: true) };

        IReadOnlyList<DivergenciaDaGovernanca> divergencias = ConferenciaDaGovernanca.DoCatalogo(Catalogo, anterior, novo, Agora);

        divergencias.Should().Contain(d => d.Tipo == TipoDeDivergencia.Classificacao && d.De == "interno" && d.Para == "sensivel");
        divergencias.Should().Contain(d => d.Tipo == TipoDeDivergencia.Escopo && d.De == "False" && d.Para == "True");
    }

    [Fact(DisplayName = "A permissão de governança sem mudança no catálogo não gera divergência")]
    public void Governanca_PermissaoIgual_SemDivergencia()
    {
        string codigo = ComposicaoDaGovernanca.Esperada[0].Codigo;
        Dictionary<string, LinhaDoRetratoDoCatalogo> catalogo = new() { [codigo] = Linha(codigo, ClassificacaoDeProtecao.Interno) };

        ConferenciaDaGovernanca.DoCatalogo(Catalogo, catalogo, catalogo, Agora).Should().BeEmpty();
    }

    [Fact(DisplayName = "A composição esperada de cada perfil de governança lista só os códigos dele")]
    public void Composicao_CodigosPorPerfil()
    {
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeAcessosId).Should().HaveCount(6);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.ProtecaoDeDadosId).Should().HaveCount(5);
        ComposicaoDaGovernanca.CodigosDe(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact(DisplayName = "As permissões de decisão protegida estão no perfil de proteção de dados e não no do gestor de acessos")]
    public void Composicao_DecisoesProtegidas_SoNaProtecaoDeDados()
    {
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.ProtecaoDeDadosId).Should().Contain(CodigosDasDecisoesProtegidas);
        ComposicaoDaGovernanca.CodigosDe(PerfisDeSistema.GestorDeAcessosId).Should().NotContain(CodigosDasDecisoesProtegidas);
    }

    [Fact(DisplayName = "Toda aprovação de proteção de dados da composição é decisão protegida e só um perfil a reúne")]
    public void Composicao_AprovacoesDeProtecao_SaoDecisoesProtegidasExclusivas()
    {
        string sufixo = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo.Split(':')[^1];

        ComposicaoDaGovernanca.Esperada.Where(p => p.Codigo.EndsWith(':' + sufixo, StringComparison.Ordinal))
            .Select(static p => p.Codigo).Should().OnlyContain(c => CodigosDasDecisoesProtegidas.Contains(c));
        ComposicaoDaGovernanca.Esperada.Where(p => CodigosDasDecisoesProtegidas.Contains(p.Codigo))
            .Should().OnlyContain(static p => p.PerfilId == PerfisDeSistema.ProtecaoDeDadosId);
    }
}
