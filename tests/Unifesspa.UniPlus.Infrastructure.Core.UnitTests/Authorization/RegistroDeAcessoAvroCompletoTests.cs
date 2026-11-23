namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using Avro.Generic;
using Avro.IO;
using Avro.Specific;

using AwesomeAssertions;

using unifesspa.uniplus.autorizacao.registrodeacesso;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization.Avro;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Registros com todos os campos preenchidos, cada um com valor próprio, lidos pelo esquema por nome, sem passar pela
/// classe gerada: o que sai do mapeador é o que um consumidor do tópico encontra em cada campo.
/// </summary>
public sealed class RegistroDeAcessoAvroCompletoTests
{
    private static readonly DateTimeOffset InstanteComMicrossegundos =
        new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

    [Fact(DisplayName = "Todo campo do registro permitido e do negado chega ao consumidor com o valor de origem")]
    public void TodoCampo_ChegaComOValorDeOrigem()
    {
        RegistroDecisaoAcesso[] origem = [Permitido(), Negado()];

        GenericRecord[] lidos = [.. LerPorNome(MapeadorDoRegistroDeAcesso.ParaLote(origem))];

        for (int i = 0; i < origem.Length; i++)
        {
            Dictionary<string, object?> esperados = Esperados(origem[i]);
            EsquemaDoRegistroDeAcesso.Registro.Fields.Select(static campo => campo.Name).Should().BeEquivalentTo(esperados.Keys);
            foreach ((string campo, object? valor) in esperados)
            {
                EmMicrossegundosSeInstante(lidos[i][campo]).Should().Be(valor, $"o campo {campo} do registro {i}");
            }
        }
    }

    [Fact(DisplayName = "Entre o permitido e o negado, nenhum campo do esquema fica sem valor")]
    public void EntreOsDois_NenhumCampoFicaNulo()
    {
        GenericRecord[] lidos = [.. LerPorNome(MapeadorDoRegistroDeAcesso.ParaLote([Permitido(), Negado()]))];

        foreach (global::Avro.Field campo in EsquemaDoRegistroDeAcesso.Registro.Fields)
        {
            lidos.Should().Contain(registro => registro[campo.Name] != null, $"o campo {campo.Name} precisa de valor em algum dos dois");
        }
    }

    [Fact(DisplayName = "O instante com microssegundos é lido em microssegundos exatos")]
    public void Instante_VoltaEmMicrossegundosExatos()
    {
        GenericRecord lido = LerPorNome(MapeadorDoRegistroDeAcesso.ParaLote([Permitido()])).Single();

        MicrossegundosDe(lido["instante"]).Should().Be(1_793_534_400_123_456L);
    }

    private static Dictionary<string, object?> Esperados(RegistroDecisaoAcesso origem) =>
        CamposDoAcesso(origem).Concat(CamposDoResource(origem)).Concat(CamposDoDesfecho(origem))
            .ToDictionary(static campo => campo.Key, static campo => campo.Value, StringComparer.Ordinal);

    private static Dictionary<string, object?> CamposDoAcesso(RegistroDecisaoAcesso origem) =>
        new()
        {
            ["instante"] = (origem.Instante.UtcTicks - DateTime.UnixEpoch.Ticks) / 10,
            ["atorEmissor"] = origem.AtorEmissor,
            ["atorContaId"] = origem.AtorContaId,
            ["requestId"] = origem.RequestId,
            ["execucaoId"] = origem.ExecucaoId.ToString("D"),
            ["permissao"] = origem.Permissao,
            ["resourceTipo"] = origem.ResourceTipo,
            ["sensibilidade"] = origem.Sensibilidade,
            ["unidadeProprietariaId"] = origem.UnidadeProprietariaId?.ToString("D"),
        };

    private static Dictionary<string, object?> CamposDoResource(RegistroDecisaoAcesso origem) =>
        new()
        {
            ["processoId"] = origem.ProcessoId?.ToString("D"),
            ["chamadaId"] = origem.ChamadaId?.ToString("D"),
            ["resourceId"] = origem.ResourceId?.ToString("D"),
            ["inscricaoId"] = origem.InscricaoId?.ToString("D"),
            ["ocorrencia"] = origem.Ocorrencia,
            ["origem"] = origem.Origem,
            ["clienteOidc"] = origem.ClienteOidc,
            ["permitido"] = origem.Permitido,
            ["motivoNegativa"] = origem.MotivoNegativa,
        };

    private static Dictionary<string, object?> CamposDoDesfecho(RegistroDecisaoAcesso origem) =>
        new()
        {
            ["fonteGrant"] = origem.FonteGrant,
            ["grantId"] = origem.GrantId?.ToString("D"),
            ["perfilId"] = origem.PerfilId?.ToString("D"),
            ["versaoId"] = origem.VersaoId?.ToString("D"),
            ["escopoUnidadeId"] = origem.EscopoUnidadeId?.ToString("D"),
            ["codigoDoDado"] = origem.FatoCodigo,
            ["resultado"] = ValoresCanonicos.De(origem.Resultado),
        };

    private static object? EmMicrossegundosSeInstante(object? valor) => valor is DateTime ? MicrossegundosDe(valor) : valor;

    private static long MicrossegundosDe(object? valor) =>
        valor is DateTime instante ? (instante.ToUniversalTime().Ticks - DateTime.UnixEpoch.Ticks) / 10 : (long)valor!;

    private static IEnumerable<GenericRecord> LerPorNome(LoteDoRegistroDeAcesso lote)
    {
        using MemoryStream fluxo = new();
        new SpecificDatumWriter<LoteDoRegistroDeAcesso>(EsquemaDoRegistroDeAcesso.Lote).Write(lote, new BinaryEncoder(fluxo));
        fluxo.Position = 0;

        GenericRecord lido = new GenericDatumReader<GenericRecord>(EsquemaDoRegistroDeAcesso.Lote, EsquemaDoRegistroDeAcesso.Lote)
            .Read(null!, new BinaryDecoder(fluxo));

        return [.. ((object[])lido["registros"]).Cast<GenericRecord>().Select(static entrada => (GenericRecord)entrada["registro"])];
    }

    private static RegistroDecisaoAcesso Permitido()
    {
        ConcessaoAtiva concessao = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Perfil de teste", Guid.CreateVersion7(), false, null, []);
        EffectiveGrant grant = EffectiveGrant.DaConcessao("selecao:inscricoes:consultar", concessao).Value!;

        return RegistroDecisaoAcesso.De(Sujeito(), Requisito(), Resource(), Requisicao(), AuthorizationDecision.Permitir(grant));
    }

    private static RegistroDecisaoAcesso Negado() =>
        RegistroDecisaoAcesso.De(
            Sujeito(), Requisito(), Resource(), Requisicao(), AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

    private static AuthorizationSubject Sujeito() =>
        AuthorizationSubject.From(UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "leandro.viana").Value!, "jti-teste").Value!;

    private static PermissionRequirement Requisito() =>
        PermissionRequirement.From("selecao:inscricoes:consultar", ClassificacaoDeProtecao.Identificador, "Lei nº 13.709/2018 (LGPD), art. 7º, III").Value!;

    private static ResourceContext Resource() =>
        ResourceContext.From(
            "inscricao",
            ClassificacaoDeProtecao.Identificador,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "COR_RACA",
            "2",
            Guid.CreateVersion7()).Value!;

    private static AuthorizationRequestContext Requisicao() =>
        AuthorizationRequestContext.From(
            "req-completo", InstanteComMicrossegundos, OrigemRequisicao.Api, execucaoId: Guid.CreateVersion7(), clienteOidc: "selecao-web").Value!;
}
