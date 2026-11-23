namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// Os candidatos atendidos das bancas requeridas por uma fase: bancas do mesmo tipo com os mesmos candidatos julgam em
/// paralelo, e só se recusa a banca sem categoria ao lado de banca com categoria e a categoria repetida.
/// </summary>
public sealed class CandidatosAtendidosPelasBancasTests
{
    private const string CodigoDaFaseComoDigitado = "  ANALISE_DOCUMENTAL_DIGITADA  ";
    private const TipoDeBanca Documental = TipoDeBanca.AnaliseDocumental;
    private const TipoDeBanca Heteroidentificacao = TipoDeBanca.Heteroidentificacao;
    private const string Renda = "RENDA";
    private const string EtnicoRacial = "ETNICO_RACIAL";

    private static BancaRequerida Banca(TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(
            TiposDeBancaCopiadosDeTeste.De(tipo),
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    private static (string? Campo, string Codigo)[] Recusas(IReadOnlyList<BancaRequerida> bancas) =>
        [.. CandidatosAtendidosPelasBancas.Violacoes(bancas).Select(static v => (v.Field, v.Error.Code))];

    [Fact(DisplayName = "Uma banca por código dispensa os candidatos atendidos")]
    public void Violacoes_UmaBancaPorCodigo_NaoViola()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao, Renda)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem bancas não há violação")]
    public void Violacoes_SemBancas_NaoViola()
    {
        Recusas([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Categoria repetida nos candidatos atendidos da banca é recusada no índice da banca")]
    public void Violacoes_CategoriaRepetidaNosCandidatosAtendidos_RecusaNoIndiceDaBanca()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao, Renda, Renda)])
            .Should().Equal(("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos"));
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo sem candidatos atendidos atendem todos os candidatos e julgam em paralelo")]
    public void Violacoes_DuasBancasDoMesmoTipoSemCandidatosAtendidos_NaoViola()
    {
        Recusas([Banca(Documental), Banca(Documental)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo com candidatos atendidos distintos são aceitas")]
    public void Violacoes_CandidatosAtendidosDistintos_NaoViola()
    {
        Recusas([Banca(Documental, Renda), Banca(Documental, EtnicoRacial)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo com os mesmos candidatos atendidos, em qualquer ordem, julgam em paralelo")]
    public void Violacoes_CandidatosAtendidosIguaisEmOutraOrdem_NaoViola()
    {
        Recusas([Banca(Documental, Renda, EtnicoRacial), Banca(Documental, EtnicoRacial, Renda)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca sem candidatos atendidos ao lado de banca com candidatos atendidos do mesmo tipo é recusada, com a citação dela")]
    public void Violacoes_BancaSemCandidatosAtendidosAoLadoDeBancaComCandidatosAtendidos_RecusaASemCandidatosAtendidos()
    {
        IReadOnlyList<FieldError> violacoes = CandidatosAtendidosPelasBancas.Violacoes([Banca(Documental, Renda), Banca(Documental)]);

        FieldError violacao = violacoes.Should().ContainSingle().Subject;
        violacao.Field.Should().Be("bancasRequeridas[1].categoriasDocumentoIds");
        violacao.Error.Code.Should().Be("BancaRequerida.CandidatosAtendidosObrigatorios");
        violacao.Error.Message.Should().Be(
            "Na fase, bancas do tipo 'Banca de análise documental' definem candidatos atendidos, e a banca 'Banca de análise documental 2' não define. Defina as categorias de cada banca desse tipo, ou retire-as de todas.");
    }

    [Fact(DisplayName = "Todas as bancas sem candidatos atendidos de um tipo misturado são recusadas, cada uma no seu índice")]
    public void Violacoes_VariasBancasSemCandidatosAtendidosNoTipoMisturado_RecusaCadaUma()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao), Banca(Documental, Renda), Banca(Documental)]).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"),
            ("bancasRequeridas[3].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"));
    }

    [Fact(DisplayName = "O agrupamento é pelo código da banca: bancas de outros códigos não entram no grupo")]
    public void Violacoes_AgrupaPeloCodigoDaBanca_IgnoraAsDeOutroCodigo()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao, Renda), Banca(Documental)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A categoria repetida cita a banca pelo rótulo do tipo e pela posição no tipo")]
    public void Violacoes_CategoriaRepetida_CitaRotuloEPosicao()
    {
        IReadOnlyList<FieldError> violacoes = CandidatosAtendidosPelasBancas.Violacoes([Banca(Documental, Renda), Banca(Documental, Renda, Renda)]);

        violacoes.Should().ContainSingle().Which.Error.Message.Should().Be(
            "Na fase, a banca 'Banca de análise documental 2' declara a mesma categoria de documento mais de uma vez nos candidatos atendidos.");
    }

    [Fact(DisplayName = "As violações vêm primeiro as de categoria repetida e depois as de banca sem categoria, cada grupo por posição")]
    public void Violacoes_Ordem_CategoriaRepetidaAntesDasSemCategoria()
    {
        BancaRequerida[] bancas =
        [
            Banca(Documental),
            Banca(Documental),
            Banca(Heteroidentificacao, Renda, Renda),
            Banca(Documental, Renda, Renda),
        ];

        Recusas(bancas).Should().Equal(
            ("bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos"),
            ("bancasRequeridas[3].categoriasDocumentoIds", "BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos"),
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"));
    }

    [Fact(DisplayName = "A recusa dos candidatos atendidos não repete o código da fase, que chega sem ser conferido")]
    public void Criar_CandidatosAtendidosRecusados_NaoRepeteOCodigoDaFase()
    {
        ModeloDeFase fase = new(CodigoDaFaseComoDigitado, 1) { Bancas = [Banca(Documental, Renda), Banca(Documental), Banca(Heteroidentificacao, Renda, Renda)] };

        Result<FaseCronograma> resultado = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!, fase.Origem(), fase.Decisoes(), fase.Publicacoes(), fase.Bancas);

        resultado.Errors.Select(static erro => erro.Error.Message).Should().HaveCount(2)
            .And.OnlyContain(mensagem => !mensagem.Contains("ANALISE_DOCUMENTAL_DIGITADA", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Lista de bancas ausente é erro de programação")]
    public void Violacoes_ListaNula_Lanca()
    {
        Func<IReadOnlyList<FieldError>> chamada = () => CandidatosAtendidosPelasBancas.Violacoes(null!);

        chamada.Should().Throw<ArgumentNullException>();
    }
}
