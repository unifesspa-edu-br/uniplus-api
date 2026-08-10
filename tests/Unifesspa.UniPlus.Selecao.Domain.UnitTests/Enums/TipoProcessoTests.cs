namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class TipoProcessoTests
{
    [Fact(DisplayName = "Getter legado retorna snapshot distinto em cada acesso")]
    public void SiSU_AcessosConsecutivos_RetornamSnapshotsDistintos()
    {
        TipoProcessoSnapshot primeiro = TipoProcesso.SiSU;
        TipoProcessoSnapshot segundo = TipoProcesso.SiSU;

        primeiro.Should().NotBeSameAs(segundo,
            "o EF mapeia o snapshot como owned e uma instância não pode pertencer a dois agregados");
        primeiro.Should().Be(segundo, "os snapshots distintos preservam o mesmo valor legado");
    }

    [Fact(DisplayName = "Identificadores dos tipos legados são UUIDv7 RFC 9562 únicos")]
    public void TiposLegados_IdentificadoresSaoUuidV7Unicos()
    {
        TipoProcessoSnapshot[] tiposLegados =
        [
            TipoProcesso.SiSU,
            TipoProcesso.PSIQ,
            TipoProcesso.PSECampo,
            TipoProcesso.PSVR,
            TipoProcesso.TransferenciaInterna,
            TipoProcesso.TransferenciaExterna,
            TipoProcesso.PortadorDiploma,
            TipoProcesso.Reopcao,
        ];

        tiposLegados.Select(tipo => tipo.OrigemId).Should().OnlyHaveUniqueItems();
        tiposLegados.Should().OnlyContain(tipo => EhUuidV7Rfc9562(tipo.OrigemId),
            "os mesmos IDs são copiados para o agregado e para o envelope canônico");
    }

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }
}
