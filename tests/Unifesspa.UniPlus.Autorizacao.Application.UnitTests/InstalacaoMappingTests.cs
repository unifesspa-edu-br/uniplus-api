namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class InstalacaoMappingTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    [Fact(DisplayName = "Prazo do código e tentativas restantes só aparecem enquanto a instalação aguarda o código")]
    public void ToDto_PrazoETentativas_SoNaEsperaDoCodigo()
    {
        Instalacao instalacao = Instalacao.Iniciar(
            new Identidade(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001"),
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().BeNull();

        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);
        instalacao.EnviarCodigo([1, 2, 3], DateTimeOffset.UtcNow);
        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().Be(Instalacao.MaximoDeTentativas);
        InstalacaoMapping.ToDto(instalacao).CodigoExpiraEm.Should().NotBeNull();

        instalacao.Cancelar(DateTimeOffset.UtcNow);
        InstalacaoMapping.ToDto(instalacao).Situacao.Should().Be(SituacaoDaInstalacao.Cancelada.ToCodigo());
        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().BeNull();
        InstalacaoMapping.ToDto(instalacao).CodigoExpiraEm.Should().BeNull();
    }
}
