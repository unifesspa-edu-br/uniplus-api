namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Xunit;

/// <summary>
/// O caráter que o tipo da etapa não admite é recusado pela declaração da etapa, com os sinalizadores do
/// snapshot do tipo: a mesma recusa vale na gravação pela entidade e na leitura do documento.
/// </summary>
public sealed class CaraterAdmitidoPeloTipoNaDeclaracaoDaEtapaTests
{
    private const string CaraterNaoAdmitido = EtapaProcessoErrorCodes.CaraterNaoAdmitidoPeloTipo;

    private static ModeloDeEtapa ClassificatoriaEmTipoQueNaoPontua(string nome = "Prova") =>
        new(nome) { TipoAdmitePontuacao = false };

    private static (string? Campo, string Codigo)[] Recusas(IEnumerable<FieldError> erros) =>
        [.. erros.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "Criar recusa a etapa classificatória cujo tipo não admite pontuação")]
    public void Criar_ClassificatoriaEmTipoQueNaoPontua_Recusa()
    {
        ModeloDeEtapa modelo = ClassificatoriaEmTipoQueNaoPontua();

        Result<EtapaProcesso> criada = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, modelo.Identificacao(), modelo.Avaliacao());

        criada.IsFailure.Should().BeTrue();
        Recusas(criada.Errors).Should().Equal(("carater", CaraterNaoAdmitido));
    }

    [Fact(DisplayName = "Atualizar os dados recusa o caráter que o tipo não admite e deixa a etapa como estava")]
    public void AtualizarDados_CaraterQueOTipoNaoAdmite_RecusaSemMutar()
    {
        EtapaProcesso etapa = new ModeloDeEtapa("Prova").Criar();
        ModeloDeEtapa recusada = ClassificatoriaEmTipoQueNaoPontua("Prova renomeada");

        Result atualizada = etapa.AtualizarDados(recusada.Identificacao(), recusada.Avaliacao());

        Recusas(atualizada.Errors).Should().Equal(("carater", CaraterNaoAdmitido));
        etapa.Nome.Should().Be("Prova");
    }

    [Fact(DisplayName = "A etapa eliminatória cujo tipo não admite eliminação é recusada")]
    public void Criar_EliminatoriaEmTipoQueNaoElimina_Recusa()
    {
        ModeloDeEtapa modelo = new ModeloDeEtapa("Prova").SemNota() with { TipoAdmiteEliminacao = false };

        Result<EtapaProcesso> criada = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, modelo.Identificacao(), modelo.Avaliacao());

        Recusas(criada.Errors).Should().Equal(("carater", CaraterNaoAdmitido));
    }

    [Fact(DisplayName = "A leitura da etapa declarada recusa o caráter que o tipo não admite e a etapa continua existindo")]
    public void Declarar_ClassificatoriaEmTipoQueNaoPontua_RecusaOCarater()
    {
        EtapaDeclarada declarada = ClassificatoriaEmTipoQueNaoPontua().Declarar();

        Recusas(declarada.Recusas).Should().Equal(("carater", CaraterNaoAdmitido));
        declarada.Recusado(DadoDaEtapa.Carater).Should().BeTrue();
    }

    [Fact(DisplayName = "Duas etapas com caráter que o tipo não admite acumulam a recusa, cada uma na posição dela")]
    public void RecusasDasDeclaracoes_DuasEtapasComCaraterNaoAdmitido_AcumulamComAPosicao()
    {
        EtapasDoProcesso etapas = new(
        [
            ClassificatoriaEmTipoQueNaoPontua("Prova 1").Declarar(),
            new ModeloDeEtapa("Prova 2").Declarar(),
            ClassificatoriaEmTipoQueNaoPontua("Prova 3").Declarar(),
        ]);

        Recusas(etapas.RecusasDasDeclaracoes()).Should().Equal(
            ("etapas[0].carater", CaraterNaoAdmitido), ("etapas[2].carater", CaraterNaoAdmitido));
    }

    [Fact(DisplayName = "A recusa de caráter vem depois das de forma e antes das de coerência da avaliação")]
    public void Violacoes_CaraterNaoAdmitidoEntreFormaECoerencia_FixaAOrdem()
    {
        ModeloDeEtapa modelo = new ModeloDeEtapa(" ") { Peso = -1m, Passo = 0.5m, TipoAdmitePontuacao = false };

        Recusas(modelo.Declarar().Recusas).Should().Equal(
            ("nome", "EtapaProcesso.NomeObrigatorio"),
            ("peso", "EtapaProcesso.PesoInvalido"),
            ("carater", CaraterNaoAdmitido),
            ("passoDoLancamento", EtapaProcessoErrorCodes.PassoNaFormaObjetiva));
    }
}
