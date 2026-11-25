namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static ApoioDaAvaliacaoDoFormulario;

public class GrupoRepetivelNaAvaliacaoTests : TestesDeAvaliacao
{
    [Fact]
    public void Grupo_AvaliaCadaOcorrencia_PelasRespostasDela()
    {
        DefinicaoGrupo grupo = Grupo(
            Obrigatoriedade.Sempre, minimo: 1, maximo: 5,
            Item("PARENTESCO"),
            Item("SOB_GUARDA", exibicao: Predicado(Condicao("PARENTESCO", Operador.Igual, "FILHO"))));

        AvaliacaoGrupo avaliacao = AvaliarDefinicao(
            ComGrupo(grupo),
            EntradaComGrupo(paginaConcluida: false, [Ocorrencia("m1", ("PARENTESCO", "FILHO")), Ocorrencia("m2", ("PARENTESCO", "MAE"))])).Grupos.Single();

        avaliacao.Ocorrencias.Select(static o => o.Itens[1].Visivel).Should().Equal(Ternario.Verdadeiro, Ternario.Falso);
        avaliacao.Ocorrencias.Select(static o => o.Estado).Should().Equal(EstadoFato.Indeterminado, EstadoFato.Resolvido);
        avaliacao.Estado.Should().Be(EstadoFato.Indeterminado, "o grupo só resolve quando todas as ocorrências resolvem");
    }

    [Fact]
    public void Grupo_OsFatosDeMembroNaoEntramNosFatosDoCandidato()
    {
        AvaliacaoFormulario avaliacao = AvaliarDefinicao(
            ComGrupo(Grupo(Obrigatoriedade.Sempre, minimo: 1, maximo: 5, Item("PARENTESCO"))),
            EntradaComGrupo(paginaConcluida: true, [Ocorrencia("m1", ("PARENTESCO", "MAE"))]));

        avaliacao.Fatos.Should().NotContainKey("PARENTESCO");
        avaliacao.Grupos.Single().Estado.Should().Be(EstadoFato.Resolvido);
        avaliacao.Grupos.Single().Ocorrencias.Single().Fatos["PARENTESCO"].Estado.Should().Be(EstadoFato.Resolvido);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void Grupo_ContagemForaDoIntervalo_NaoValeComoResposta(int quantas, bool valida)
    {
        DefinicaoGrupo grupo = Grupo(Obrigatoriedade.Sempre, minimo: 2, maximo: 3, Item("PARENTESCO"));
        (string, (string, object)[])[] ocorrencias = [.. Enumerable.Range(0, quantas).Select(i => Ocorrencia($"m{i}", ("PARENTESCO", "MAE")))];

        AvaliacaoGrupo avaliacao = AvaliarDefinicao(ComGrupo(grupo), EntradaComGrupo(paginaConcluida: true, ocorrencias)).Grupos.Single();

        avaliacao.ContagemValida.Should().Be(valida);
        avaliacao.Estado.Should().Be(valida ? EstadoFato.Resolvido : EstadoFato.Indeterminado);
    }

    [Fact]
    public void Grupo_SemMaximo_AceitaQualquerQuantidadeDeOcorrencias()
    {
        DefinicaoGrupo grupo = Grupo(Obrigatoriedade.Sempre, minimo: 1, maximo: null, Item("PARENTESCO"));
        (string, (string, object)[])[] ocorrencias = [.. Enumerable.Range(0, 1000).Select(i => Ocorrencia($"m{i}", ("PARENTESCO", "MAE")))];

        AvaliacaoGrupo avaliacao = AvaliarDefinicao(ComGrupo(grupo), EntradaComGrupo(paginaConcluida: true, ocorrencias)).Grupos.Single();

        avaliacao.ContagemValida.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, EstadoFato.Resolvido)]
    [InlineData(1, EstadoFato.Indeterminado)]
    public void GrupoObrigatorio_ListaVazia_ResolveSoComMinimoZero(int minimo, EstadoFato esperado)
    {
        AvaliacaoGrupo avaliacao = AvaliarDefinicao(
            ComGrupo(Grupo(Obrigatoriedade.Sempre, minimo, maximo: 5, Item("PARENTESCO"))),
            EntradaComGrupo(paginaConcluida: true, [])).Grupos.Single();

        avaliacao.Estado.Should().Be(esperado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GrupoOpcional_ListaVazia_EhNaoInformado_MesmoComMinimoAcimaDeZero(bool paginaConcluida)
    {
        AvaliacaoGrupo avaliacao = AvaliarDefinicao(
            ComGrupo(Grupo(Obrigatoriedade.Nunca, minimo: 2, maximo: 5, Item("PARENTESCO"))),
            EntradaComGrupo(paginaConcluida, [])).Grupos.Single();

        avaliacao.ContagemValida.Should().BeTrue("o opcional aceita zero ocorrência");
        avaliacao.Estado.Should().Be(EstadoFato.NaoInformado, "a lista vazia é resposta, mesmo com a página aberta");
    }

    [Theory]
    [InlineData(true, EstadoFato.NaoInformado)]
    [InlineData(false, EstadoFato.Indeterminado)]
    public void GrupoOpcional_SemResposta_SoViraNaoInformadoComAPaginaConcluida(bool paginaConcluida, EstadoFato esperado)
    {
        AvaliacaoGrupo avaliacao = AvaliarDefinicao(
            ComGrupo(Grupo(Obrigatoriedade.Nunca, minimo: 0, maximo: 5, Item("PARENTESCO"))),
            Entrada(paginaConcluida)).Grupos.Single();

        avaliacao.Estado.Should().Be(esperado);
    }

    [Fact]
    public void Grupo_OcorrenciasComAMesmaIdentidade_SaoRecusadas()
    {
        Action avaliar = () => AvaliarDefinicao(
            ComGrupo(Grupo(Obrigatoriedade.Sempre, minimo: 1, maximo: 5, Item("PARENTESCO"))),
            EntradaComGrupo(paginaConcluida: true, [Ocorrencia("m1", ("PARENTESCO", "MAE")), Ocorrencia("m1", ("PARENTESCO", "PAI"))]));

        avaliar.Should().Throw<ArgumentException>().WithMessage("*'m1'*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ocorrencia_SemIdentidade_EhRecusada(string id)
    {
        Action criar = () => _ = new OcorrenciaRespondida(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal));

        criar.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(new[] { "PROPRIO_CANDIDATO", "PAI_OU_MAE" }, true)]
    [InlineData(new[] { "PAI_OU_MAE" }, false)]
    [InlineData(new[] { "PROPRIO_CANDIDATO", "PROPRIO_CANDIDATO" }, false)]
    public void GrupoQueIncluiOCandidato_ValeSoComExatamenteUmaOcorrenciaDele(string[] parentescos, bool valida)
    {
        DefinicaoGrupo grupo = new("COMPOSICAO", exibicao: null, Obrigatoriedade.Sempre, minimo: 1, maximo: null, [Item("PARENTESCO")], incluiCandidato: true);
        (string, (string, object)[])[] ocorrencias = [.. parentescos.Select((p, i) => Ocorrencia($"m{i}", ("PARENTESCO", p)))];

        AvaliacaoGrupo avaliacao = AvaliarDefinicao(ComGrupo(grupo), EntradaComGrupo(paginaConcluida: true, ocorrencias)).Grupos.Single();

        avaliacao.OcorrenciaDoCandidatoValida.Should().Be(valida);
        avaliacao.Estado.Should().Be(valida ? EstadoFato.Resolvido : EstadoFato.Indeterminado);
    }

    [Fact]
    public void GrupoOpcionalQueIncluiOCandidato_ListaVazia_EhNaoInformado()
    {
        DefinicaoGrupo grupo = new("COMPOSICAO", exibicao: null, Obrigatoriedade.Nunca, minimo: 1, maximo: null, [Item("PARENTESCO")], incluiCandidato: true);

        AvaliacaoGrupo avaliacao = AvaliarDefinicao(ComGrupo(grupo), EntradaComGrupo(paginaConcluida: false, [])).Grupos.Single();

        avaliacao.OcorrenciaDoCandidatoValida.Should().BeTrue("a lista vazia não tem ocorrência do candidato a conferir");
        avaliacao.Estado.Should().Be(EstadoFato.NaoInformado);
    }

    private static DefinicaoFormulario FormularioComGrupoExibidoSoParaQuemConcorrePorRenda() => new(
        [new DefinicaoPagina(
            Pagina,
            exibicao: null,
            [Item("CONCORRER_RENDA")],
            [new DefinicaoGrupo(
                "COMPOSICAO",
                Predicado(Condicao("CONCORRER_RENDA", Operador.Igual, true)),
                Obrigatoriedade.Sempre,
                minimo: 1,
                maximo: 5,
                [Item("PARENTESCO")],
                incluiCandidato: false)])],
        termos: [],
        derivacoes: []);

    [Fact]
    public void GrupoOculto_EhNaoAplicavel_MesmoComOcorrenciaGravada()
    {
        AvaliacaoGrupo avaliacao = AvaliarDefinicao(
            FormularioComGrupoExibidoSoParaQuemConcorrePorRenda(),
            EntradaComGrupo(paginaConcluida: true, [Ocorrencia("m1", ("PARENTESCO", "MAE"))], ("CONCORRER_RENDA", false))).Grupos.Single();

        avaliacao.Visivel.Should().Be(Ternario.Falso);
        avaliacao.Estado.Should().Be(EstadoFato.NaoAplicavel);
        avaliacao.Ocorrencias.Should().BeEmpty();
    }

    private static DefinicaoGrupo Grupo(Obrigatoriedade obrigatoriedade, int minimo, int? maximo, params DefinicaoItem[] subitens) =>
        new("COMPOSICAO", exibicao: null, obrigatoriedade, minimo, maximo, subitens, incluiCandidato: false);

    private static DefinicaoFormulario ComGrupo(DefinicaoGrupo grupo) =>
        new([new DefinicaoPagina(Pagina, exibicao: null, [], [grupo])], termos: [], derivacoes: []);

    private static (string Id, (string Fato, object Valor)[] Respostas) Ocorrencia(string id, params (string Fato, object Valor)[] respostas) =>
        (id, respostas);

    /// <summary>A entrada com as ocorrências do grupo <c>COMPOSICAO</c>, na ordem dada, e as respostas do candidato.</summary>
    private static EntradaAvaliacaoFormulario EntradaComGrupo(
        bool paginaConcluida,
        IReadOnlyList<(string Id, (string Fato, object Valor)[] Respostas)> ocorrencias,
        params (string Fato, object Valor)[] respostas) =>
        Entrada(paginaConcluida, respostas) with
        {
            RespostasDosGrupos = new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO"] = [.. ocorrencias.Select(static o => new OcorrenciaRespondida(o.Id, Respostas(o.Respostas)))],
            },
        };
}
