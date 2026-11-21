namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

/// <summary>
/// Os candidatos atendidos das bancas requeridas por uma fase: obrigatório e distinto só onde o tipo
/// da banca não basta para identificá-la.
/// </summary>
public sealed class CandidatosAtendidosPelasBancasTests
{
    private const string Fase = "ANALISE_DOCUMENTAL";
    private const string Documental = "BANCA_ANALISE_DOCUMENTAL";
    private const string Heteroidentificacao = "BANCA_HETEROIDENTIFICACAO";
    private const string Renda = "RENDA";
    private const string EtnicoRacial = "ETNICO_RACIAL";

    private static BancaRequerida Banca(string codigo, params string[] categorias) =>
        BancaRequerida.Criar(
            Guid.CreateVersion7(), codigo, "Banca de teste",
            [.. categorias.Select(static c => CategoriaJulgada.Criar(Guid.CreateVersion7(), c))]);

    private static (string? Campo, string Codigo)[] Recusas(IReadOnlyList<BancaRequerida> bancas) =>
        [.. CandidatosAtendidosPelasBancas.Violacoes(Fase, bancas).Select(static v => (v.Field, v.Error.Code))];

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

    [Fact(DisplayName = "Duas bancas do mesmo código sem candidatos atendidos são recusadas, cada uma no seu índice")]
    public void Violacoes_DuasBancasIguaisSemCandidatosAtendidos_CandidatosAtendidosPedidosNasDuas()
    {
        Recusas([Banca(Documental), Banca(Documental)]).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"));
    }

    [Fact(DisplayName = "Duas bancas do mesmo código com candidatos atendidos distintos são aceitas")]
    public void Violacoes_CandidatosAtendidosDistintos_NaoViola()
    {
        Recusas([Banca(Documental, Renda), Banca(Documental, EtnicoRacial)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Candidatos atendidos iguais declarados em outra ordem não distinguem as bancas")]
    public void Violacoes_CandidatosAtendidosIguaisEmOutraOrdem_RecusaASegunda()
    {
        IReadOnlyList<FieldError> violacoes = CandidatosAtendidosPelasBancas.Violacoes(
            Fase, [Banca(Documental, Renda, EtnicoRacial), Banca(Documental, EtnicoRacial, Renda)]);

        FieldError violacao = violacoes.Should().ContainSingle().Subject;
        violacao.Field.Should().Be("bancasRequeridas[1].categoriasDocumentoIds");
        violacao.Error.Code.Should().Be("BancaRequerida.CandidatosAtendidosDuplicados");
        violacao.Error.Message.Should().Contain($"a banca '{Documental}' atende os mesmos candidatos");
    }

    [Fact(DisplayName = "O agrupamento é pelo código da banca: bancas de outros códigos não entram no grupo")]
    public void Violacoes_AgrupaPeloCodigoDaBanca_IgnoraAsDeOutroCodigo()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao), Banca(Documental)]).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"),
            ("bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"));
    }

    [Fact(DisplayName = "As violações vêm primeiro as de categoria repetida, por índice, e depois as dos grupos")]
    public void Violacoes_Ordem_CategoriaRepetidaAntesDosGrupos()
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

    [Fact(DisplayName = "Lista de bancas ausente é erro de programação")]
    public void Violacoes_ListaNula_Lanca()
    {
        Func<IReadOnlyList<FieldError>> chamada = () => CandidatosAtendidosPelasBancas.Violacoes(Fase, null!);

        chamada.Should().Throw<ArgumentNullException>();
    }
}
