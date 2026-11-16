namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDeInscricaoPublicada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.ProcessoComMatrizDaLeiDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.PublicacaoDoProcessoDeTeste;

/// <summary>Salvar e enviar recusam, com o código de domínio, duas ocorrências do mesmo grupo com o mesmo id, em vez de lançar.</summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class GrupoComIdRepetidoNoPedidoTests
{
    private readonly CenarioDeInscricaoPublicada _cenario;

    public GrupoComIdRepetidoNoPedidoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDeInscricaoPublicada(fixture);

    [Fact(DisplayName = "Salvar recusa duas ocorrências do mesmo grupo com o mesmo id com o código de id duplicado, sem lançar")]
    public async Task Salvar_ComIdDeOcorrenciaRepetido_RecusaSemLancar()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Salvar_ComIdDeOcorrenciaRepetido_RecusaSemLancar));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = ComOcorrenciasRepetidas(processo);

        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(db, processoId, pedido, etag);

        salvar.IsFailure.Should().BeTrue();
        salvar.Errors.Should().Contain(static e => e.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Enviar recusa duas ocorrências do mesmo grupo com o mesmo id com o código de id duplicado, sem lançar")]
    public async Task Enviar_ComIdDeOcorrenciaRepetido_RecusaSemLancar()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Enviar_ComIdDeOcorrenciaRepetido_RecusaSemLancar));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = ComOcorrenciasRepetidas(processo);

        Result<ReciboDaInscricaoDto> envio = await _cenario.EnviarAsync(db, processoId, pedido, etag, "00000901");

        envio.IsFailure.Should().BeTrue();
        envio.Errors.Should().Contain(static e => e.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Salvar recusa a ocorrência de grupo com id em branco com o código de id duplicado, sem lançar")]
    public async Task Salvar_ComIdDeOcorrenciaEmBranco_RecusaSemLancar()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Salvar_ComIdDeOcorrenciaEmBranco_RecusaSemLancar));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = ComOcorrencias(processo, "  ", "outra");

        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(db, processoId, pedido, etag);

        salvar.Errors.Should().Contain(static e => e.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Recusa de forma que não é de identidade segue depois da precondição: com If-Match desatualizado, a precondição prevalece")]
    public async Task Salvar_ComGrupoForaDoFormularioEEtagDesatualizado_RecusaAPrecondicao()
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        ProcessoSeletivo processo = ProcessoComAMatrizNormativa(nameof(Salvar_ComGrupoForaDoFormularioEEtagDesatualizado_RecusaAPrecondicao));
        (Guid processoId, _) = await PublicarEDivulgarAsync(db, processo, OpcoesDaOrigemEscolar());
        await _cenario.IniciarComoFernandaAsync(db, processoId);
        InscricaoRequest pedido = PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true)) with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["GRUPO_QUE_NAO_EXISTE"] = [new OcorrenciaDeGrupoInscricaoInput("a", null)],
            },
        };

        Result<InscricaoDto> salvar = await _cenario.SalvarAsync(db, processoId, pedido, "\"desatualizado\"");

        salvar.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
    }

    private static InscricaoRequest ComOcorrenciasRepetidas(ProcessoSeletivo processo) => ComOcorrencias(processo, "mesma", "mesma");

    private static InscricaoRequest ComOcorrencias(ProcessoSeletivo processo, string primeiroId, string segundoId) =>
        PedidoDeEnvio(processo, RespostasDaOrigemEDasCotas(OrigemEscolar.SomentePublica, ep: true, ppi: true, renda: true)) with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new OcorrenciaDeGrupoInscricaoInput(primeiroId, null),
                    new OcorrenciaDeGrupoInscricaoInput(segundoId, null),
                ],
            },
        };
}
