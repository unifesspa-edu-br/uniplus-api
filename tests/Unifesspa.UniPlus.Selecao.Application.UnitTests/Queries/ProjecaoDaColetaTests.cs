namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura do que o processo coleta devolve as opções declaradas agrupadas por fato, os fatos em ordem
/// de código e as opções de cada fato pela ordem declarada, e a referência temporal dos fatos como foi salva.
/// </summary>
public sealed class ProjecaoDaColetaTests
{
    private static readonly Guid FaseDeReferenciaId = new("01a0d640-0000-7000-8000-000000000c01");
    private static readonly DateOnly DataDeReferencia = new(2027, 2, 15);

    [Fact(DisplayName = "Processo sem opções declaradas devolve a lista de opções vazia")]
    public void ProjetarOpcoesDeclaradas_SemOpcoes_DevolveListaVazia()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("Processo sem opções", OrigemCandidatos.InscricaoPropria);

        ProjecaoDaColeta.ProjetarOpcoesDeclaradas(processo).Should().BeEmpty();
    }

    [Fact(DisplayName = "As opções declaradas saem agrupadas por fato, e os fatos saem em ordem de código")]
    public void ProjetarOpcoesDeclaradas_VariosFatos_AgrupaPorFatoEmOrdemDeCodigo()
    {
        ProcessoSeletivo processo = ProcessoComOpcoesDeDoisFatos();

        IReadOnlyList<OpcoesDeclaradasDoFatoDto> dtos = ProjecaoDaColeta.ProjetarOpcoesDeclaradas(processo);

        dtos.Select(static d => d.FatoCodigo).Should().Equal("CIDADE_DE_PROVA", "IDIOMA_DA_PROVA");
        dtos[0].Opcoes.Should().HaveCount(2);
        dtos[1].Opcoes.Should().ContainSingle().Which.Codigo.Should().Be("INGLES");
    }

    [Fact(DisplayName = "As opções de cada fato saem pela ordem declarada, com código, rótulo e ordem")]
    public void ProjetarOpcoesDeclaradas_OpcoesForaDeOrdem_SaemPelaOrdemDeclarada()
    {
        ProcessoSeletivo processo = ProcessoComOpcoesDeDoisFatos();

        OpcoesDeclaradasDoFatoDto cidades = ProjecaoDaColeta.ProjetarOpcoesDeclaradas(processo)[0];

        cidades.Opcoes.Should().Equal(
            new OpcaoDoProcessoDto("MARABA", "Marabá", 1), new OpcaoDoProcessoDto("ALTAMIRA", "Altamira", 2));
    }

    [Fact(DisplayName = "Sem referência temporal declarada a projeção devolve referência ausente")]
    public void ProjetarReferenciaTemporal_SemReferencia_DevolveNulo()
    {
        ProjecaoDaColeta.ProjetarReferenciaTemporal(null).Should().BeNull();
    }

    [Fact(DisplayName = "A referência temporal de data específica devolve o tipo e a data, sem fase")]
    public void ProjetarReferenciaTemporal_DataEspecifica_DevolveTipoEData()
    {
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, DataDeReferencia, null).Value!;

        ReferenciaTemporalFatosDto? dto = ProjecaoDaColeta.ProjetarReferenciaTemporal(referencia);

        dto.Should().Be(new ReferenciaTemporalFatosDto("DATA_ESPECIFICA", DataDeReferencia, null));
    }

    [Fact(DisplayName = "A referência temporal de fim de fase devolve o tipo e a fase, sem data")]
    public void ProjetarReferenciaTemporal_FimDeFase_DevolveTipoEFase()
    {
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.FimFase, null, FaseDeReferenciaId).Value!;

        ReferenciaTemporalFatosDto? dto = ProjecaoDaColeta.ProjetarReferenciaTemporal(referencia);

        dto.Should().Be(new ReferenciaTemporalFatosDto("FIM_FASE", null, FaseDeReferenciaId));
    }

    private static ProcessoSeletivo ProcessoComOpcoesDeDoisFatos()
    {
        ProcessoSeletivo processo = ProcessosDeTeste.SemConfiguracao("Processo com opções", OrigemCandidatos.InscricaoPropria);
        processo.DefinirOpcoesDeclaradas("IDIOMA_DA_PROVA", [Opcao("IDIOMA_DA_PROVA", "INGLES", "Inglês", 1)]).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeclaradas(
            "CIDADE_DE_PROVA",
            [Opcao("CIDADE_DE_PROVA", "ALTAMIRA", "Altamira", 2), Opcao("CIDADE_DE_PROVA", "MARABA", "Marabá", 1)]).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static OpcaoDeclaradaFato Opcao(string fato, string codigo, string rotulo, int ordem) =>
        OpcaoDeclaradaFato.Criar(fato, codigo, rotulo, ordem).Value!;
}
