namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Xunit;

/// <summary>
/// O recorte de competência das bancas requeridas por uma fase: obrigatório e distinto só onde o tipo
/// da banca não basta para identificá-la.
/// </summary>
public sealed class RecorteDeCompetenciaDasBancasTests
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
        [.. RecorteDeCompetenciaDasBancas.Violacoes(Fase, bancas).Select(static v => (v.Field, v.Error.Code))];

    [Fact(DisplayName = "Uma banca por código dispensa o recorte")]
    public void Violacoes_UmaBancaPorCodigo_NaoViola()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao, Renda)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem bancas não há violação")]
    public void Violacoes_SemBancas_NaoViola()
    {
        Recusas([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Categoria repetida no recorte da banca é recusada no índice da banca")]
    public void Violacoes_CategoriaRepetidaNoRecorte_RecusaNoIndiceDaBanca()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao, Renda, Renda)])
            .Should().Equal(("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.CategoriaDuplicadaNoRecorte"));
    }

    [Fact(DisplayName = "Duas bancas do mesmo código sem recorte são recusadas, cada uma no seu índice")]
    public void Violacoes_DuasBancasIguaisSemRecorte_RecortePedidoNasDuas()
    {
        Recusas([Banca(Documental), Banca(Documental)]).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"));
    }

    [Fact(DisplayName = "Duas bancas do mesmo código com recortes distintos são aceitas")]
    public void Violacoes_RecortesDistintos_NaoViola()
    {
        Recusas([Banca(Documental, Renda), Banca(Documental, EtnicoRacial)]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Recortes iguais declarados em outra ordem não distinguem as bancas")]
    public void Violacoes_RecortesIguaisEmOutraOrdem_RecusaASegunda()
    {
        IReadOnlyList<FieldError> violacoes = RecorteDeCompetenciaDasBancas.Violacoes(
            Fase, [Banca(Documental, Renda, EtnicoRacial), Banca(Documental, EtnicoRacial, Renda)]);

        FieldError violacao = violacoes.Should().ContainSingle().Subject;
        violacao.Field.Should().Be("bancasRequeridas[1].categoriasDocumentoIds");
        violacao.Error.Code.Should().Be("BancaRequerida.RecorteDeCompetenciaDuplicado");
        violacao.Error.Message.Should().Contain($"({EtnicoRacial}, {Renda})");
    }

    [Fact(DisplayName = "O agrupamento é pelo código da banca: bancas de outros códigos não entram no grupo")]
    public void Violacoes_AgrupaPeloCodigoDaBanca_IgnoraAsDeOutroCodigo()
    {
        Recusas([Banca(Documental), Banca(Heteroidentificacao), Banca(Documental)]).Should().Equal(
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"),
            ("bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"));
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
            ("bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.CategoriaDuplicadaNoRecorte"),
            ("bancasRequeridas[3].categoriasDocumentoIds", "BancaRequerida.CategoriaDuplicadaNoRecorte"),
            ("bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"),
            ("bancasRequeridas[1].categoriasDocumentoIds", "BancaRequerida.RecorteDeCompetenciaObrigatorio"));
    }

    [Fact(DisplayName = "Lista de bancas ausente é erro de programação")]
    public void Violacoes_ListaNula_Lanca()
    {
        Func<IReadOnlyList<FieldError>> chamada = () => RecorteDeCompetenciaDasBancas.Violacoes(Fase, null!);

        chamada.Should().Throw<ArgumentNullException>();
    }
}
