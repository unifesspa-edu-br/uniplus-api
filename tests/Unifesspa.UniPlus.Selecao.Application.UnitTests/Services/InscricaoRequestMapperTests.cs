namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;


using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A anotação de nulidade não alcança os elementos de uma coleção genérica: o corpo com a lista de um
/// grupo nula, ou com uma ocorrência nula, desserializa sem erro e tem de ser recusado na leitura.
/// </summary>
public sealed class InscricaoRequestMapperTests
{
    [Fact(DisplayName = "ParaGrupos recusa a lista de grupo nula no caminho do grupo")]
    public void ParaGrupos_ListaNula_RecusaNoCaminhoDoGrupo()
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new(StringComparer.Ordinal)
        {
            ["DEPENDENTES"] = null!,
        };

        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> resultado = InscricaoRequestMapper.ParaGrupos(grupos);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("DEPENDENTES");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.GrupoMalFormado);
    }

    [Fact(DisplayName = "ParaGrupos recusa a ocorrência nula no caminho da ocorrência e segue lendo as demais")]
    public void ParaGrupos_OcorrenciaNula_RecusaNoCaminhoDaOcorrencia()
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new(StringComparer.Ordinal)
        {
            ["DEPENDENTES"] =
            [
                new OcorrenciaDeGrupoInscricaoInput("a", null),
                null!,
            ],
            ["OUTRO"] = null!,
        };

        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> resultado = InscricaoRequestMapper.ParaGrupos(grupos);

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("DEPENDENTES[1]", "OUTRO");
        resultado.Errors.Should().OnlyContain(static e => e.Error.Code == InscricaoErrorCodes.GrupoMalFormado);
    }

    [Fact(DisplayName = "ParaGrupos lê o grupo bem formado, com a ocorrência sem respostas valendo vazia")]
    public void ParaGrupos_GrupoBemFormado_Le()
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> grupos = new(StringComparer.Ordinal)
        {
            ["DEPENDENTES"] = [new OcorrenciaDeGrupoInscricaoInput("a", null)],
        };

        Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> resultado = InscricaoRequestMapper.ParaGrupos(grupos);

        resultado.IsSuccess.Should().BeTrue();
        OcorrenciaDeGrupoInscricao ocorrencia = resultado.Value!["DEPENDENTES"].Should().ContainSingle().Subject;
        ocorrencia.Id.Should().Be("a");
        ocorrencia.Respostas.Should().BeEmpty();
    }
}
