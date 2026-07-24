namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="ObterProcessoSeletivoQueryHandler.Handle"/> para a leitura tipada de
/// fatos coletados e regras de derivação (Story #987): a ordenação determinística, o contrato
/// "ausência é null, nunca []" e o round-trip do valor tipado.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerColetaDeFatosTests
{
    private static CondicaoPrecondicaoFato Precondicao(int clausula, string fato, Operador operador, object valor) =>
        CondicaoPrecondicaoFato.Criar(clausula, fato, operador, JsonSerializer.SerializeToElement(valor)).Value!;

    private static CondicaoRegraDerivacao CondicaoRegra(int clausula, string fato, Operador operador, object valor) =>
        CondicaoRegraDerivacao.Criar(clausula, fato, operador, JsonSerializer.SerializeToElement(valor)).Value!;

    private static async Task<ProcessoSeletivoDto> ProjetarAsync(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);
        dto.Should().NotBeNull();
        return dto!;
    }

    [Fact(DisplayName = "Fatos coletados são ordenados por ordem; sem pré-condição projeta null, nunca []")]
    public async Task Fatos_OrdenadosEPrecondicaoNullNuncaVazia()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        // BAIXA_RENDA na ordem 0 (sem pré-condição); COR_RACA na ordem 1 com pré-condição citando o anterior.
        FatoColetado baixaRenda = FatoColetado.Criar("BAIXA_RENDA", 0, null).Value!;
        FatoColetado corRaca = FatoColetado.Criar("COR_RACA", 1,
            [Precondicao(0, "BAIXA_RENDA", Operador.Igual, true)]).Value!;
        // Passa fora de ordem de propósito — a projeção é quem ordena.
        processo.DefinirFatosColetados([corRaca, baixaRenda]).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        dto.FatosColetados.Select(f => f.FatoCodigo).Should().ContainInOrder("BAIXA_RENDA", "COR_RACA");
        dto.FatosColetados[0].Precondicao.Should().BeNull("fato sem pré-condição projeta null, nunca lista vazia");

        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? precondicao = dto.FatosColetados[1].Precondicao;
        precondicao.Should().NotBeNull();
        precondicao!.Should().ContainSingle().Which.Should().ContainSingle();
        CondicaoPrecondicaoDto condicao = precondicao[0][0];
        condicao.Fato.Should().Be("BAIXA_RENDA");
        condicao.Operador.Should().Be("IGUAL");
        condicao.Valor.GetBoolean().Should().BeTrue("o valor tipado faz round-trip como booleano JSON");
    }

    [Fact(DisplayName = "Regras de derivação: config por codigoFato, regras por ordem; âncora projeta quando null")]
    public async Task Regras_OrdenadasEAncoraProjetaNull()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        RegraDerivacaoConfigurada ancora = RegraDerivacaoConfigurada.Criar(0, "AC", null).Value!;
        RegraDerivacaoConfigurada condicional = RegraDerivacaoConfigurada.Criar(1, "LB_PPI",
            [CondicaoRegra(0, "COR_RACA", Operador.Igual, "PRETA")]).Value!;
        // Regras fora de ordem — a projeção ordena por Ordem.
        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [condicional, ancora]).Value!;
        processo.DefinirRegrasDerivacao([config]).IsSuccess.Should().BeTrue();

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        ConfiguracaoDerivacaoDto projetada = dto.RegrasDerivacao.Should().ContainSingle().Which;
        projetada.CodigoFato.Should().Be("MODALIDADE");
        projetada.Regras.Select(r => r.Ordem).Should().ContainInOrder(0, 1);

        RegraDerivacaoDto regraAncora = projetada.Regras[0];
        regraAncora.Contribui.Should().Be("AC");
        regraAncora.Quando.Should().BeNull("a regra âncora incondicional projeta quando null, nunca lista vazia");

        RegraDerivacaoDto regraCondicional = projetada.Regras[1];
        regraCondicional.Contribui.Should().Be("LB_PPI");
        regraCondicional.Quando.Should().NotBeNull();
        CondicaoDerivacaoDto condicao = regraCondicional.Quando![0][0];
        condicao.Fato.Should().Be("COR_RACA");
        condicao.Operador.Should().Be("IGUAL");
        condicao.Valor.GetString().Should().Be("PRETA");
    }

    [Fact(DisplayName = "Processo sem coleta nem derivação projeta listas vazias (não null)")]
    public async Task SemColeta_ListasVazias()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Query", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        ProcessoSeletivoDto dto = await ProjetarAsync(processo);

        dto.FatosColetados.Should().BeEmpty();
        dto.RegrasDerivacao.Should().BeEmpty();
    }
}
