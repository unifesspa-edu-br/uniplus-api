namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDaColetaDeFatos;

/// <summary>As ocorrências do grupo repetível — mínimo, inclusão do candidato e máximo — recusadas no handler de coleta de fatos.</summary>
public sealed class OcorrenciasDoGrupoNaColetaDeFatosTests
{
    [Fact(DisplayName = "Grupo sem as ocorrências é recusado no mínimo e na inclusão do candidato, e não é criado")]
    public async Task Handle_GrupoSemOcorrencias_RecusaOMinimoEAInclusao()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Ocorrencias = null };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Select(static e => e.Field).Should().Equal("grupos[0].ocorrencias.minimo", "grupos[0].ocorrencias.incluiCandidato");
        resultado.Errors.Select(static e => e.Error.Code).Should().AllBe(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        processo.GruposColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas das ocorrências vêm antes das da forma do grupo, na ordem do pedido")]
    public async Task Handle_GrupoSemOcorrenciasEComCodigoInvalido_RecusasDasOcorrenciasPrimeiro()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Ocorrencias = null, Codigo = " " };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Select(static e => e.Field).Should().ContainInOrder(
            "grupos[0].ocorrencias.minimo", "grupos[0].ocorrencias.incluiCandidato", "grupos[0].codigo");
        processo.GruposColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O mínimo ausente com a inclusão do candidato é recusado uma vez, sem a recusa de ao menos uma ocorrência")]
    public async Task Handle_MinimoAusenteComInclusaoDoCandidato_RecusaUmaVez()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Ocorrencias = new OcorrenciasDoGrupoInput(null, true, 10) };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("grupos[0].ocorrencias.minimo");
        recusa.Error.Code.Should().Be(GrupoFormularioErrorCodes.OcorrenciasObrigatorias);
        processo.GruposColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O mínimo ausente com o máximo zero recusa o mínimo e o máximo, nessa ordem")]
    public async Task Handle_MinimoAusenteComMaximoZero_RecusaOMinimoEOMaximo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Ocorrencias = new OcorrenciasDoGrupoInput(null, false, 0) };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Select(static e => e.Field).Should().Equal("grupos[0].ocorrencias.minimo", "grupos[0].ocorrencias.maximo");
        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(GrupoFormularioErrorCodes.OcorrenciasObrigatorias, GrupoFormularioErrorCodes.ContagemIncoerente);
    }

    [Fact(DisplayName = "O mínimo ausente com máximo válido e sem o candidato é recusado sozinho")]
    public async Task Handle_MinimoAusenteSemInclusao_RecusaSoOMinimo()
    {
        ProcessoSeletivo processo = ProcessoEmRascunho();
        Mocks mocks = NovosMocks(processo, processo.Id);
        GrupoColetadoInput grupo = Composicao() with { Ocorrencias = new OcorrenciasDoGrupoInput(null, false, 10) };

        Result resultado = await HandleAsync(mocks, ComGrupo(processo, grupo));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("grupos[0].ocorrencias.minimo");
    }
}
