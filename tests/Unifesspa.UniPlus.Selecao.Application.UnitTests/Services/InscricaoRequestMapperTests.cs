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

    [Fact(DisplayName = "ParaAceites recusa o aceite nulo no caminho do aceite e segue lendo os demais")]
    public void ParaAceites_AceiteNulo_RecusaNoCaminhoDoAceite()
    {
        Result<List<AceiteDeTermo>> resultado = InscricaoRequestMapper.ParaAceites(
        [
            new AceiteDeTermoInput("LGPD", Guid.NewGuid(), "hash"),
            null!,
        ]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteMalFormado);
    }

    [Fact(DisplayName = "ParaAceites recusa o aceite sem código ou sem hash no caminho do campo, porque a gravação exige os dois")]
    public void ParaAceites_AceiteSemCodigoOuSemHash_RecusaNoCaminhoDoCampo()
    {
        Result<List<AceiteDeTermo>> resultado = InscricaoRequestMapper.ParaAceites(
        [
            new AceiteDeTermoInput(null!, Guid.NewGuid(), "hash"),
            new AceiteDeTermoInput("LGPD", Guid.NewGuid(), null!),
        ]);

        resultado.Errors.Select(static e => e.Field).Should().BeEquivalentTo("aceites[0].codigo", "aceites[1].hashVersao");
        resultado.Errors.Should().OnlyContain(static e => e.Error.Code == InscricaoErrorCodes.AceiteMalFormado);
    }

    [Fact(DisplayName = "ParaAceites lê o aceite bem formado e trata a lista ausente como vazia")]
    public void ParaAceites_AceiteBemFormado_Le()
    {
        Guid versao = Guid.NewGuid();

        Result<List<AceiteDeTermo>> lido = InscricaoRequestMapper.ParaAceites([new AceiteDeTermoInput("LGPD", versao, "hash")]);
        Result<List<AceiteDeTermo>> ausente = InscricaoRequestMapper.ParaAceites(null);

        lido.Value.Should().ContainSingle().Which.Should().Be(new AceiteDeTermo("LGPD", versao, "hash"));
        ausente.Value.Should().BeEmpty();
    }
}
