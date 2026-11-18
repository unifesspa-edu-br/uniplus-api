namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static CenarioDeInscricaoPublicada;
using static ProcessosDeInscricaoDeTeste;
using static PublicacaoDoProcessoDeTeste;

[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class RecusaDoEnvioAposAdocaoTests(InscricaoDbFixture fixture)
{
    private readonly CenarioDeInscricaoPublicada _cenario = new(fixture);

    [Fact]
    public async Task Enviar_VersaoAdotadaComRespostaObrigatoriaAusente_DescartaAdocaoAntesDaGravacaoPosterior()
    {
        (Guid processoId, InscricaoDto original, InscricaoRequest pedido) = await PrepararAsync();
        await using UniPlusDbContext db = _cenario.CriarDbContext();

        Result<ReciboDaInscricaoDto> recusado = await _cenario.EnviarAsync(db, processoId, pedido, original.ETag, "00000901");
        recusado.Errors.Should().Contain(e => e.Field == "NOVA");
        await db.SalvarAlteracoesAsync(CancellationToken.None);

        (Inscricao relida, IReadOnlyList<ComprovanteInscricao> comprovantes) = await _cenario.RelerAsync(processoId);
        relida.Formulario.VersaoFormularioId.Should().Be(original.Formulario.VersaoFormularioId);
        relida.ETag.Should().Be(original.ETag);
        relida.Formulario.Preenchimento.Respostas.Should().ContainKey("ANTIGA");
        comprovantes.Should().BeEmpty();
    }

    private async Task<(Guid ProcessoId, InscricaoDto Original, InscricaoRequest Pedido)> PrepararAsync()
    {
        const string Nome = nameof(RecusaDoEnvioAposAdocaoTests);
        ProcessoSeletivo antiga = ProcessoComUmCampo(Nome, "ANTIGA", Obrigatoriedade.Nunca);
        ProcessoSeletivo nova = ProcessoQueDerivaAModalidadeComUmCampo(Nome, "NOVA");
        (Guid processoId, DadosEdital dados, InscricaoDto original) = await SalvarOriginalAsync(antiga);
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        await DivulgarRetificacaoAsync(db, processoId, nova, dados);
        InscricaoRequest pedido = PedidoDeEnvio(nova, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = JsonSerializer.SerializeToElement(true),
        });
        return (processoId, original, pedido);
    }

    private async Task<(Guid ProcessoId, DadosEdital Dados, InscricaoDto Original)> SalvarOriginalAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext db = _cenario.CriarDbContext();
        (Guid processoId, DadosEdital dados) = await PublicarEDivulgarAsync(db, processo);
        string etag = await _cenario.IniciarComoFernandaAsync(db, processoId);
        Result<InscricaoDto> salvo = await _cenario.SalvarAsync(db, processoId,
            new InscricaoRequest(null, new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["ANTIGA"] = JsonSerializer.SerializeToElement("resposta"),
            }, null, null), etag);
        salvo.IsSuccess.Should().BeTrue(salvo.Error?.Message);
        return (processoId, dados, salvo.Value!);
    }
}
