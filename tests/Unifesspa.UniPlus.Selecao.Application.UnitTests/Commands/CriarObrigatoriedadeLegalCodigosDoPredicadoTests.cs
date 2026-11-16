namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDaObrigatoriedadeLegal;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.LeitoresDeCadastroParaObrigatoriedadeLegal;

/// <summary>
/// Os códigos que o predicado cita são conferidos contra o cadastro vivo, e persistidos como o cadastro os
/// grava: o avaliador compara por igualdade exata contra o código congelado no processo.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCodigosDoPredicadoTests
{
    private readonly CenarioDaObrigatoriedadeLegal _cenario = new();

    private async Task<ObrigatoriedadeLegal?> CriarPersistindoAsync(PredicadoObrigatoriedade predicado)
    {
        ObrigatoriedadeLegal? persistida = null;
        await _cenario.Repositorio.AdicionarAsync(Arg.Do<ObrigatoriedadeLegal>(r => persistida = r), Arg.Any<CancellationToken>());
        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar(predicado));
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return persistida;
    }

    private async Task<string> RecusaDoPredicadoAsync(PredicadoObrigatoriedade predicado)
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar(predicado));
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        return resultado.Error!.Code;
    }

    [Fact(DisplayName = "Modalidade inexistente no predicado de documento é recusada sem persistir")]
    public async Task Handle_ModalidadeInexistente_Recusa()
    {
        _cenario.Modalidades = ModalidadeReaderSem("LB_PPl");

        (await RecusaDoPredicadoAsync(new DocumentoObrigatorioParaModalidade("LB_PPl", "LAUDO_MEDICO")))
            .Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
    }

    [Fact(DisplayName = "Tipo de documento inexistente é recusado com erro distinto do de modalidade")]
    public async Task Handle_TipoDocumentoInexistente_RecusaComErroProprio()
    {
        _cenario.TiposDeDocumento = TipoDocumentoReaderSem("LAUDO_INEXISTENTE");

        (await RecusaDoPredicadoAsync(new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_INEXISTENTE")))
            .Should().Be("ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado");
    }

    [Fact(DisplayName = "Um código órfão no meio da lista de modalidades mínimas recusa a regra inteira")]
    public async Task Handle_ModalidadesMinimasComCodigoOrfao_Recusa()
    {
        _cenario.Modalidades = ModalidadeReaderSem("LB_PPl");

        (await RecusaDoPredicadoAsync(new ModalidadesMinimas(["AC", "LB_PPl", "LB_Q"])))
            .Should().Be("ObrigatoriedadeLegal.ModalidadeNaoEncontrada");
    }

    [Fact(DisplayName = "Códigos do predicado são persistidos normalizados, não como vieram no payload")]
    public async Task Handle_CodigosComEspaco_PersisteNormalizado()
    {
        ObrigatoriedadeLegal? persistida = await CriarPersistindoAsync(new DocumentoObrigatorioParaModalidade(" LB_PPI ", " LAUDO_MEDICO "));

        persistida!.Predicado.Should().Be(new DocumentoObrigatorioParaModalidade("LB_PPI", "LAUDO_MEDICO"));
    }

    [Fact(DisplayName = "Exigência de modalidades mínimas sem nenhum código é recusada pela forma")]
    public async Task Handle_ModalidadesMinimasVazia_Recusa()
    {
        (await RecusaDoPredicadoAsync(new ModalidadesMinimas([]))).Should().Be("ObrigatoriedadeLegal.ModalidadesMinimasVazia");
    }

    [Fact(DisplayName = "Código em branco no predicado recusa pela forma, antes de consultar o cadastro")]
    public async Task Handle_CodigoEmBranco_RecusaPelaFormaAntesDeConsultarOCadastro()
    {
        ITipoEtapaReader tipos = _cenario.TiposDeEtapa = Substitute.For<ITipoEtapaReader>();

        (await RecusaDoPredicadoAsync(new EtapaObrigatoria("   "))).Should().Be("ObrigatoriedadeLegal.PredicadoComCodigoEmBranco");
        await tipos.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Código de tipo de documento é buscado como o cadastro o grava, sem recompor a forma Unicode")]
    public async Task Handle_TipoDocumentoEmFormaDecomposta_BuscaOCodigoComoVeio()
    {
        const string Decomposto = "LAUDO_MÉDICO";
        ITipoDocumentoReader tipos = _cenario.TiposDeDocumento = Substitute.For<ITipoDocumentoReader>();
        tipos.ObterVivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>()).Returns(CadastrosVivos.TipoDocumento(Decomposto));

        await CriarPersistindoAsync(new DocumentoObrigatorioParaModalidade("LB_PPI", $" {Decomposto} "));

        await tipos.Received().ObterVivoPorCodigoAsync(Decomposto, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regra que exige tipo de deficiência inexistente é recusada na escrita")]
    public async Task Handle_TipoDeficienciaInexistente_Recusa()
    {
        _cenario.TiposDeDeficiencia = CadastrosVivos.TiposDeficiencia("DEFICIENCIA_VISUAL");

        (await RecusaDoPredicadoAsync(new AtendimentoDisponivel(["TIPO_QUE_NAO_EXISTE"])))
            .Should().Be("ObrigatoriedadeLegal.TipoDeficienciaNaoEncontrada");
    }

    [Fact(DisplayName = "Regra que exige critério de desempate fora do catálogo é recusada na escrita")]
    public async Task Handle_CriterioDesempateInexistente_Recusa()
    {
        _cenario.RegrasDeDesempate = CadastrosVivos.RegrasDesempate("IDADE_MAIOR");

        (await RecusaDoPredicadoAsync(new DesempateDeveIncluir("CRITERIO_INVENTADO")))
            .Should().Be("ObrigatoriedadeLegal.CriterioDesempateNaoEncontrado");
    }

    [Fact(DisplayName = "Exigência de atendimento sem nenhum código é recusada pela forma")]
    public async Task Handle_AtendimentoDisponivelVazio_Recusa()
    {
        (await RecusaDoPredicadoAsync(new AtendimentoDisponivel([]))).Should().Be("ObrigatoriedadeLegal.AtendimentoDisponivelVazio");
    }

    [Fact(DisplayName = "Código de tipo de deficiência com espaço supérfluo é normalizado antes de persistir")]
    public async Task Handle_CodigoComEspaco_Normaliza()
    {
        _cenario.TiposDeDeficiencia = CadastrosVivos.TiposDeficiencia("DEFICIENCIA_VISUAL");

        ObrigatoriedadeLegal? persistida = await CriarPersistindoAsync(new AtendimentoDisponivel([" DEFICIENCIA_VISUAL "]));

        persistida!.Predicado.Should().BeOfType<AtendimentoDisponivel>().Which.Necessidades.Should().Equal(["DEFICIENCIA_VISUAL"]);
    }

    [Fact(DisplayName = "Critério de desempate com caractere decomponível é encontrado como o catálogo o grava")]
    public async Task Handle_CriterioDesempateDecomposto_Encontra()
    {
        const string CodigoDecomposto = "CRITERIO_ANTIGUIDADÉ";
        _cenario.RegrasDeDesempate = CadastrosVivos.RegrasDesempate(CodigoDecomposto);

        ObrigatoriedadeLegal? persistida = await CriarPersistindoAsync(new DesempateDeveIncluir($" {CodigoDecomposto} "));

        persistida!.Predicado.Should().Be(new DesempateDeveIncluir(CodigoDecomposto));
    }
}
