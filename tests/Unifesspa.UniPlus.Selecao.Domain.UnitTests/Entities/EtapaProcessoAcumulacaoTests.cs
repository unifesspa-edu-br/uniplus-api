namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioEtapaProcesso;

public sealed class EtapaProcessoAcumulacaoTests
{
    [Fact(DisplayName = "A contradição da escala acumula com as demais recusas, cada uma no seu campo")]
    public void ValidarFormaBasica_ContradicaoDaEscala_AcumulaComAsDemais()
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Ambas, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 0m, NotaMinima: 12m, NotaMaxima: 10m, PassoDoLancamento: 0.3m, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

        erros.Select(e => e.Field).Should().BeEquivalentTo(["peso", "notaMinima", "notaMaxima"]);
    }

    [Fact(DisplayName = "Criar com ordem não positiva falha com OrdemInvalida")]
    public void Criar_OrdemInvalida_Falha()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 0, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.OrdemInvalida");
    }

    [Fact(DisplayName = "ADR-0125: todas as violações simultâneas acumulam no mesmo lote")]
    public void Criar_ViolacoesSimultaneas_AcumulaTodasNoMesmoLote()
    {
        Result<EtapaProcesso> resultado = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: string.Empty, Carater: (CaraterEtapa)99, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 0, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 0m, NotaMinima: -1m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "EtapaProcesso.NomeObrigatorio",
            "EtapaProcesso.CaraterObrigatorio",
            "EtapaProcesso.PesoInvalido",
            "EtapaProcesso.NotaMinimaInvalida",
            "EtapaProcesso.OrdemInvalida",
        ]);
    }

    /// <summary>
    /// Peso em etapa que não pontua fica gravado sem nunca entrar no divisor da média: quem
    /// configurou acredita que pesa, e o cálculo o ignora.
    /// </summary>
    [Fact(DisplayName = "Peso em etapa apenas eliminatória é recusado")]
    public void ValidarFormaBasica_PesoEmEtapaApenasEliminatoria_Recusa()
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Banca de heteroidentificação", CaraterEtapa.Eliminatoria, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.PesoSemCaraterQuePontua");
        erros[0].Field.Should().Be("peso");
    }

    [Fact(DisplayName = "Nota mínima em etapa apenas classificatória é recusada")]
    public void ValidarFormaBasica_NotaMinimaEmEtapaApenasClassificatoria_Recusa()
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Classificatoria, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.NotaMinimaSemCaraterQueElimina");
        erros[0].Field.Should().Be("notaMinima");
    }

    /// <summary>
    /// Caráter ausente já é reportado por si; um segundo erro dizendo que o peso não combina com
    /// ele mandaria a pessoa corrigir um campo que não está errado.
    /// </summary>
    [Fact(DisplayName = "Caráter ausente não gera também erro de coerência de peso")]
    public void ValidarFormaBasica_CaraterAusenteComPeso_ReportaSoOCarater()
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Etapa", null, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

        erros.Should().ContainSingle()
            .Which.Error.Code.Should().Be("EtapaProcesso.CaraterObrigatorio");
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violação retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacao_Vazio()
    {
        List<FieldError> erros = ConferenciaDaDeclaracaoDaEtapa.ValidarFormaBasica(
            "Prova Objetiva", CaraterEtapa.Ambas, ordem: 1,
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3m, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "AtualizarDados com dados válidos tem sucesso e substitui os campos")]
    public void AtualizarDados_DadosValidos_Sucesso()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

        Result resultado = etapa.AtualizarDados(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva (revisada)", Carater: CaraterEtapa.Ambas, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2m, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null));

        resultado.IsSuccess.Should().BeTrue();
        etapa.Nome.Should().Be("Prova Objetiva (revisada)");
        etapa.Carater.Should().Be(CaraterEtapa.Ambas);
        etapa.Peso.Should().Be(2m);
        etapa.NotaMinima.Should().Be(5m);
        etapa.Ordem.Should().Be(2);
    }

    [Fact(DisplayName = "AtualizarDados com nome vazio falha e não muda o estado anterior")]
    public void AtualizarDados_NomeVazio_FalhaSemMutarEstado()
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

        Result resultado = etapa.AtualizarDados(
            new IdentificacaoDaEtapa(Nome: "   ", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().Contain("EtapaProcesso.NomeObrigatorio");
        etapa.Nome.Should().Be("Prova Objetiva");
    }
}
