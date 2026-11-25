namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ModalidadeSelecionadaRecusasAcumuladasTests
{
    private static ModalidadeNoCadastro Cadastro() => ModalidadesNoCadastroDeTeste.Da(
        "AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas);

    private static void Conferir(Result<ModalidadeSelecionada> resultado, params (string Codigo, string Mensagem)[] esperadas)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Error.Code, e.Error.Message)).Should().Equal(esperadas);
        resultado.Errors.Should().OnlyContain(e => e.Field == null);
        resultado.Errors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();
        resultado.Error.Should().Be(resultado.Errors[0].Error);
    }

    [Fact]
    public void Criar_IdentidadeECodigoVazios_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { Id = Guid.Empty, Codigo = "", BaseLegal = "" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio", "Identidade da modalidade de origem é obrigatória."),
            ("ModalidadeSelecionada.CodigoObrigatorio", "Código da modalidade é obrigatório."),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."));
    }

    [Fact]
    public void Criar_CadastroIncompleto_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { NaturezaLegal = null, ComposicaoVagas = null, BaseLegal = "", ComposicaoOrigemCodigo = "X" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.NaturezaLegalObrigatoria", "Natureza legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.ComposicaoVagasObrigatoria", "Composição de vagas da modalidade é obrigatória."),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."));
    }

    [Fact]
    public void Criar_BaseECruzado_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { BaseLegal = "", RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", "Modalidade com remanejamento CRUZADO exige a modalidade par."));
    }

    [Fact]
    public void Criar_BaseEOrigemAusentes_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { BaseLegal = "", ComposicaoVagas = ComposicaoVagasModalidade.RetiradaDeOutraModalidade, ComposicaoOrigemCodigo = null };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.ComposicaoOrigemObrigatoria", "Modalidade com composição RETIRADA_DE_OUTRA_MODALIDADE exige o código da modalidade de origem."));
    }

    [Fact]
    public void Criar_OrigemEParIndevidos_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { ComposicaoOrigemCodigo = "X", RemanejamentoPar = "P" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.ComposicaoOrigemIndevida", "A modalidade só declara código de origem quando a composição de vagas é RETIRADA_DE_OUTRA_MODALIDADE."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO."));
    }

    [Fact]
    public void Criar_CotaSemDestino_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { NaturezaLegal = NaturezaLegalModalidade.CotaReservada, RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.CotaReservadaExigeCascata", "A modalidade é cota reservada da Lei 12.711 e deve seguir a ordem legal de remanejamento (SEGUE_ORDEM_DE_REMANEJAMENTO)."));
    }

    [Fact]
    public void Criar_CotaEBase_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { NaturezaLegal = NaturezaLegalModalidade.CotaReservada, BaseLegal = "" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.CotaReservadaExigeCascata", "A modalidade é cota reservada da Lei 12.711 e deve seguir a ordem legal de remanejamento (SEGUE_ORDEM_DE_REMANEJAMENTO)."));
    }

    [Fact]
    public void Criar_DestinoUnicoSemDestinoEComPar_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { RegraRemanejamento = RegraRemanejamentoModalidade.DestinoUnico, RemanejamentoPar = "P" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.RemanejamentoDestinoObrigatorio", "Modalidade com remanejamento DESTINO_UNICO exige o destino."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO."));
    }

    [Fact]
    public void Criar_CruzadoSemParEComDestino_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado, RemanejamentoDestino = "D" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", "Modalidade com remanejamento CRUZADO exige a modalidade par."),
            ("ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO."));
    }

    [Fact]
    public void Criar_SemRegraComDestinoEPar_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { RemanejamentoDestino = "D", RemanejamentoPar = "P" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO."));
    }

    [Fact]
    public void Criar_NaturezaAusenteECruzado_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { NaturezaLegal = null, RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.NaturezaLegalObrigatoria", "Natureza legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", "Modalidade com remanejamento CRUZADO exige a modalidade par."));
    }

    [Fact]
    public void Criar_ComposicaoNaoReconhecidaComOrigem_RecusaSomenteComposicao()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { ComposicaoVagas = (ComposicaoVagasModalidade)999, ComposicaoOrigemCodigo = "X" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.ComposicaoVagasObrigatoria", "Composição de vagas da modalidade é obrigatória."));
    }

    [Fact]
    public void Criar_NaturezaNaoReconhecidaComParametrosInvalidos_RecusaNaturezaEParametros()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { NaturezaLegal = (NaturezaLegalModalidade)999, RemanejamentoDestino = "D", RemanejamentoPar = "P" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.NaturezaLegalObrigatoria", "Natureza legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", "A modalidade só declara par ou destino final de remanejamento quando a regra é CRUZADO."));
    }

    [Fact]
    public void Criar_TodosOsDefeitos_RecusaNaOrdem()
    {
        ModalidadeNoCadastro cadastro = Cadastro() with { Id = Guid.Empty, NaturezaLegal = null, ComposicaoVagas = null, BaseLegal = "", RegraRemanejamento = RegraRemanejamentoModalidade.Cruzado, RemanejamentoDestino = "D" };
        Conferir(ModalidadeSelecionada.Criar(cadastro),
            ("ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio", "Identidade da modalidade de origem é obrigatória."),
            ("ModalidadeSelecionada.NaturezaLegalObrigatoria", "Natureza legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.ComposicaoVagasObrigatoria", "Composição de vagas da modalidade é obrigatória."),
            ("ModalidadeSelecionada.BaseLegalObrigatoria", "Base legal da modalidade é obrigatória."),
            ("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", "Modalidade com remanejamento CRUZADO exige a modalidade par."),
            ("ModalidadeSelecionada.RemanejamentoDestinoIndevido", "A modalidade só declara destino de remanejamento quando a regra é DESTINO_UNICO."));
    }

}
