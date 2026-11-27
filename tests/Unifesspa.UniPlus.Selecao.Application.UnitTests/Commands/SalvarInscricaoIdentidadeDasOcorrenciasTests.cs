namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A identidade das ocorrências de grupo é conferida antes de a gravação conferir a precondição: sem ela a avaliação não roda.
/// As demais recusas do corpo só aparecem depois da precondição.
/// </summary>
public sealed class SalvarInscricaoIdentidadeDasOcorrenciasTests
{
    private static readonly PrecondicaoIfMatch EtagVencido = PrecondicaoIfMatch.DeTags(["\"vencido\""]);

    [Fact(DisplayName = "Id repetido com campo fora do grupo e If-Match vencido recusa só a identidade, uma vez")]
    public async Task Handle_IdRepetidoComPrecondicaoVencida_RecusaSoAIdentidade()
    {
        SalvarRespostaDeTeste cenario = Cenario();

        Result<InscricaoDto> resultado = await cenario.SalvarAsync(ComMembros("membro-1", "membro-1"), precondicao: EtagVencido);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("COMPOSICAO_FAMILIAR.membro-1", InscricaoErrorCodes.GrupoIdDuplicado));
    }

    [Fact(DisplayName = "Sem id repetido, o campo fora do grupo com If-Match vencido recusa pela precondição")]
    public async Task Handle_SemIdRepetidoComPrecondicaoVencida_RecusaPelaPrecondicao()
    {
        SalvarRespostaDeTeste cenario = Cenario();

        Result<InscricaoDto> resultado = await cenario.SalvarAsync(ComMembros("membro-1", "membro-2"), precondicao: EtagVencido);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
    }

    private static SalvarRespostaDeTeste Cenario() =>
        SalvarRespostaDeTeste.ComItem(
            FatoColetadoDeTeste.Coletando("APELIDO", 0).ComRotulo("Apelido").DeTexto("LIVRE").ComObrigatoriedade(Obrigatoriedade.Nunca).Construir(),
            [GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 1).ComOcorrencias(0, null).ComObrigatoriedade(Obrigatoriedade.Nunca)
                .ComCampos(FatoColetadoDeTeste.Coletando("NOME_MEMBRO", 0).ComRotulo("Nome do membro").DeTexto("LIVRE").ComObrigatoriedade(Obrigatoriedade.Nunca).Construir())
                .Construir()],
            PersonasDeTeste.KevinCarvalho);

    private static InscricaoRequest ComMembros(params string[] ids) => new(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
    {
        ["COMPOSICAO_FAMILIAR"] = [.. ids.Select(static id => new OcorrenciaDeGrupoInscricaoInput(id, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["CAMPO_FORA"] = JsonSerializer.SerializeToElement("x"),
        }))],
    }, null);
}
