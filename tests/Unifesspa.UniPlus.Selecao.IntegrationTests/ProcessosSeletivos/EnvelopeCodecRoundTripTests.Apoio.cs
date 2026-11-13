namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed partial class EnvelopeCodecRoundTripTests
{
    private const string SchemaCorrente = "0.0.42";

    private static (ProcessoSeletivo Processo, FaseCronograma Fase) ProcessoDeInscricao(string titulo)
    {
        FaseCronograma fase = ProcessoBaseDeEnvelope.FaseUnica("INSCRICAO", "INSCRICAO", permiteComplementacao: true);
        return (ProcessoBaseDeEnvelope.Montar(titulo, fase), fase);
    }

    private static DadosEdital DadosDeEditalDoEnvelope() =>
        DadosEdital.Criar(
            numero: "099/2026",
            periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
            documentoEditalId: Guid.CreateVersion7()).Value!;

    private static void ConferirSchemaCorrente(SnapshotCanonico congelado) =>
        congelado.SchemaVersion.Should().Be(SchemaCorrente, "pré-condição: o codec corrente emite a forma única");

    private static VersaoConfiguracao Publicado(
        ProcessoSeletivo processo, EntradaCanonicalizacao entrada, SnapshotCanonico congelado, string sub)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            entrada.Dados, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
            entrada.HashDocumento, sub, TimeProvider.System, CorpusEnvelope.ContextoRico(),
            FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        return publicacao.Value!;
    }

    private static EnvelopeReidratado Reidratado(VersaoConfiguracao versao)
    {
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        return reidratado.Value!;
    }

    private static byte[] Recodificado(ProcessoSeletivo processo, VersaoConfiguracao versao, EnvelopeReidratado envelope) =>
        CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo,
                envelope.Dados,
                envelope.HashDocumento, FusoInstitucional.ZoneId,
                envelope.Retificacao,
                envelope.Conformidade,
                envelope.MetadadosFatosCongelados,
                envelope.ValoresSelecionaveisCongelados,
                envelope.CalendarioDiasUteis, envelope.AgregadosDosGrupos, envelope.ApresentacaoDosPressupostos,
                TiposDeAtoCongelados: envelope.TiposDeAtoCongelados, FatosDerivadosCongelados: envelope.FatosDerivadosCongelados)).Value!.Bytes;

    private static void ConferirQueARecodificacaoReproduzOsBytes(
        byte[] recodificado, SnapshotCanonico congelado, string objeto) =>
        recodificado.Should().Equal(
            congelado.Bytes,
            $"reidratar e recanonicalizar {objeto} tem de reproduzir os bytes congelados inteiros — " +
            "qualquer campo perdido pelo decoder sai daqui como divergência");
}
