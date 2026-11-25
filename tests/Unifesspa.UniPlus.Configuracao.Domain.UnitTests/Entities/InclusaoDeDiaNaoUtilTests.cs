namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.DiaNaoUtilCriacaoDeTeste;

public sealed class InclusaoDeDiaNaoUtilTests
{
    [Fact(DisplayName = "IncluirDiaNaoUtil com item válido adiciona ao agregado e preserva os demais")]
    public void IncluirDiaNaoUtil_ItemValido_AdicionaEPreservaDemais()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(Estadual());

        resultado.IsSuccess.Should().BeTrue();
        calendario.DiasNaoUteis.Should().HaveCount(2);
        calendario.DiasNaoUteis.Should().Contain(d => d.Abrangencia == Abrangencia.Nacional);
        calendario.DiasNaoUteis.Should().Contain(d => d.Abrangencia == Abrangencia.Estadual && d.Uf == "PA");
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil preserva Id, VersaoDataset e Vigente do calendário")]
    public void IncluirDiaNaoUtil_ItemValido_PreservaIdentidadeDoCalendario()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        calendario.MarcarVigente();
        Guid idOriginal = calendario.Id;

        calendario.IncluirDiaNaoUtil(Estadual());

        calendario.Id.Should().Be(idOriginal);
        calendario.VersaoDataset.Should().Be("2027.1");
        calendario.Vigente.Should().BeTrue();
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil em calendário vigente e em rascunho tem o mesmo comportamento")]
    public void IncluirDiaNaoUtil_CalendarioRascunho_TemSucesso()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(Estadual());

        resultado.IsSuccess.Should().BeTrue();
        calendario.Vigente.Should().BeFalse();
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil com item nulo falha com DiaNaoUtilNulo rotulado como \"item\"")]
    public void IncluirDiaNaoUtil_ItemNulo_Falha()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("item");
        resultado.Errors[0].Error.Code.Should().Be(CalendarioDiasUteisErrorCodes.DiaNaoUtilNulo);
        calendario.DiasNaoUteis.Should().HaveCount(1);
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil com data no valor default (0001-01-01) falha")]
    public void IncluirDiaNaoUtil_DataDefault_Falha()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(
            new DiaNaoUtilCriacao("ESTADUAL", null, null, null, default, "Dia sem data", "PA"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataAusente);
        calendario.DiasNaoUteis.Should().HaveCount(1);
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil com abrangência inválida falha e não persiste nada")]
    public void IncluirDiaNaoUtil_AbrangenciaInvalida_Falha()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(
            new DiaNaoUtilCriacao("INVALIDO", null, null, null, DataBase.AddDays(10), "Dia qualquer"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.AbrangenciaInvalida);
        calendario.DiasNaoUteis.Should().HaveCount(1);
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil com combinação já cadastrada no agregado falha com DataDuplicadaNoDataset")]
    public void IncluirDiaNaoUtil_DuplicataContraAgregadoJaPersistido_Falha()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar(
            "2027.1", [Municipal(municipioIbge: "1501402", municipioNome: "Belém", municipioUf: "PA")]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(
            Municipal(municipioIbge: "1501402", municipioNome: "Belém", municipioUf: "PA"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
        calendario.DiasNaoUteis.Should().HaveCount(1);
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil com mesma data e abrangências diferentes não é duplicata")]
    public void IncluirDiaNaoUtil_MesmaDataAbrangenciaDiferente_NaoEDuplicata()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(
            new DiaNaoUtilCriacao("ESTADUAL", null, null, null, DataBase, "Feriado estadual", "PA"));

        resultado.IsSuccess.Should().BeTrue();
        calendario.DiasNaoUteis.Should().HaveCount(2);
    }

    [Fact(DisplayName = "IncluirDiaNaoUtil chamado duas vezes com a mesma combinação recusa a segunda")]
    public void IncluirDiaNaoUtil_ChamadoDuasVezesComMesmaCombinacao_RecusaASegunda()
    {
        CalendarioDiasUteis calendario = CalendarioDiasUteis.Criar("2027.1", [Nacional()]).Value!;
        DiaNaoUtilCriacao item = Estadual();
        calendario.IncluirDiaNaoUtil(item);

        Result<DiaNaoUtil> resultado = calendario.IncluirDiaNaoUtil(item);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
        calendario.DiasNaoUteis.Should().HaveCount(2);
    }
}
