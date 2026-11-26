namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static CenarioDeFasesDoProcessoPublicado;

internal sealed class ApoioDaUnidadeDaFaseNaVersaoVigente
{
    private readonly ProcessoSeletivoDbFixture _fixture;
    private readonly CenarioDeFasesDoProcessoPublicado _cenario;

    internal ApoioDaUnidadeDaFaseNaVersaoVigente(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
        _cenario = new CenarioDeFasesDoProcessoPublicado(fixture);
    }

    internal async Task SemearImportacaoDeNotasAsync(Guid processoId, Guid etapaId, DateTimeOffset importadoEm)
    {
        await using UniPlusDbContext semeadura = _fixture.CreateDbContext();
        Guid versaoId = await semeadura.VersoesConfiguracao.Where(v => v.ProcessoSeletivoId == processoId).Select(v => v.Id).FirstAsync();
        semeadura.LotesDeNotasDoEnem.Add(LoteDeNotasDoEnem.Registrar(
            processoId, etapaId, "2026", "INEP_ENEM_RETORNO", "v1", new string('b', 64), new string('c', 64), "conta-admin", importadoEm, 0,
            versaoId, new ResultadoDoCruzamento([], [], [], []), []));
        await semeadura.SaveChangesAsync();
    }

    internal async Task SemearInscricaoRecebidaAsync(Guid processoId, DateTimeOffset criadaEm)
    {
        await using UniPlusDbContext semeadura = _fixture.CreateDbContext();
        semeadura.Inscricoes.Add(InscricaoSubmetida(processoId, criadaEm));
        await semeadura.SaveChangesAsync();
    }

    internal static InscricaoRecord InscricaoSubmetida(Guid processoId, DateTimeOffset criadaEm)
    {
        Guid id = Guid.CreateVersion7();
        return new InscricaoRecord
        {
            Id = id,
            ProcessoSeletivoId = processoId,
            CpfIndice = id.ToByteArray(),
            ContaId = $"conta-{id:N}",
            EmissorToken = "https://issuer",
            Situacao = SituacaoInscricao.Enviada,
            VersaoFormularioId = Guid.CreateVersion7(),
            NumeroInscricao = $"UNI-{id.ToString("N")[^8..]}",
            Revisao = 1,
            CreatedAt = criadaEm,
        };
    }

    internal static string InstanteCanonico(int ano, int mes, int dia) =>
        HashCanonicalComputer.SerializeInstantCanonical(new DateTimeOffset(ano, mes, dia, 0, 0, 0, TimeSpan.Zero));

    internal static void JanelaNoPassado(JsonObject fase)
    {
        fase["inicio"] = InstanteCanonico(2020, 1, 1);
        fase["fim"] = InstanteCanonico(2020, 1, 31);
    }

    // A fase nova nasce com a janela no futuro, para só a inscrição ou a importação poderem iniciá-la.
    internal static JsonObject FaseNovaComAJanelaNoFuturo(JsonObject envelope, Guid origemId)
    {
        JsonArray fases = (JsonArray)envelope["cronogramaFases"]!["fases"]!;
        JsonObject nova = JsonNode.Parse(fases[0]!.ToJsonString())!.AsObject();
        nova["id"] = Guid.CreateVersion7();
        nova["tipoFaseOrigemId"] = origemId;
        nova["codigo"] = "SEGUNDA_FASE";
        AdiaAJanela(nova);
        fases.Add(nova);
        return nova;
    }

    internal static void AcrescentaFaseQueColeta(JsonObject envelope, Guid origemId) =>
        FaseNovaComAJanelaNoFuturo(envelope, origemId)["coletaInscricao"] = true;

    internal static void MoveAEtapaParaNovaFase(JsonObject envelope, Guid origemId)
    {
        FaseNovaComAJanelaNoFuturo(envelope, origemId)["coletaInscricao"] = false;
        envelope["etapas"]!.AsArray()[0]!["faseCodigo"] = "SEGUNDA_FASE";
    }

    internal static void AdiaAJanela(JsonObject fase)
    {
        fase["inicio"] = InstanteCanonico(2045, 1, 1);
        fase["fim"] = InstanteCanonico(2045, 1, 31);
    }

    internal Task SucederAsync(Guid processoId, DateTimeOffset vigenteAPartirDe, Action<JsonObject> alteraAFase) =>
        SucederNoEnvelopeAsync(processoId, vigenteAPartirDe, envelope => alteraAFase((JsonObject)envelope["cronogramaFases"]!["fases"]![0]!));

    internal async Task SucederNoEnvelopeAsync(Guid processoId, DateTimeOffset vigenteAPartirDe, Action<JsonObject> alteraOEnvelope)
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        VersaoConfiguracao anterior = await context.VersoesConfiguracao
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        JsonObject envelope = JsonNode.Parse(anterior.ConfiguracaoCongelada)!.AsObject();
        alteraOEnvelope(envelope);
        context.VersoesConfiguracao.Add(VersaoConfiguracao.Suceder(
            anterior, new DocumentoCongelado(Encoding.UTF8.GetBytes(envelope.ToJsonString()), anterior.SchemaVersion, anterior.AlgoritmoHash, anterior.AtoCriadorHash),
            Guid.CreateVersion7(), "plataforma-admin", vigenteAPartirDe));
        await context.SaveChangesAsync();
    }

    internal static void AcrescentaEtapaIrma(JsonObject envelope, Guid id)
    {
        JsonArray etapas = envelope["etapas"]!.AsArray();
        JsonObject irma = JsonNode.Parse(etapas[0]!.ToJsonString())!.AsObject();
        irma["id"] = id;
        irma["nome"] = "Análise documental";
        irma["inicio"] = null;
        irma["fim"] = null;
        etapas.Add(irma);
    }

    internal static void AbreAJanelaDaEtapaEAcrescentaAIrma(JsonObject envelope, Guid irma)
    {
        AcrescentaEtapaIrma(envelope, irma);
        envelope["etapas"]!.AsArray()[0]!["inicio"] = InstanteCanonico(2020, 1, 1);
    }

    internal async Task SemearEnvioAoInepAsync(Guid processoId, Guid etapaId)
    {
        await using UniPlusDbContext semeadura = _fixture.CreateDbContext();
        semeadura.GeracoesDeEnvioAoInep.Add(GeracaoDeEnvioAoInep.Registrar(
            processoId, etapaId, "2026", "INEP_ENEM_ENVIO", "v1", new string('d', 64), "conta-admin", DateTimeOffset.UtcNow, 0));
        await semeadura.SaveChangesAsync();
    }
}
