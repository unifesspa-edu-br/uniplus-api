namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class InstalacaoMappingTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    [Fact(DisplayName = "Antes do envio do código a instalação não mostra prazo nem tentativas restantes")]
    public void ToDto_AntesDoCodigo_SemPrazoNemTentativas()
    {
        Instalacao instalacao = Iniciada();

        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().BeNull();
        InstalacaoMapping.ToDto(instalacao).CodigoExpiraEm.Should().BeNull();
    }

    [Fact(DisplayName = "Aguardando o código a instalação mostra o prazo e as tentativas restantes")]
    public void ToDto_AguardandoCodigo_MostraPrazoETentativas()
    {
        Instalacao instalacao = AguardandoCodigo();

        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().Be(Instalacao.MaximoDeTentativas);
        InstalacaoMapping.ToDto(instalacao).CodigoExpiraEm.Should().NotBeNull();
    }

    [Fact(DisplayName = "Cancelada a instalação deixa de mostrar o prazo e as tentativas restantes")]
    public void ToDto_Cancelada_SemPrazoNemTentativas()
    {
        Instalacao instalacao = AguardandoCodigo();

        instalacao.Cancelar(DateTimeOffset.UtcNow);

        InstalacaoMapping.ToDto(instalacao).Situacao.Codigo.Should().Be("CANCELADA");
        InstalacaoMapping.ToDto(instalacao).TentativasRestantes.Should().BeNull();
        InstalacaoMapping.ToDto(instalacao).CodigoExpiraEm.Should().BeNull();
    }

    [Fact(DisplayName = "A instalação iniciada mostra a situação Aguardando documento como código e rótulo")]
    public void ToDto_Iniciada_MostraSituacaoComRotulo()
    {
        InstalacaoDto dto = InstalacaoMapping.ToDto(Iniciada());

        (dto.Situacao.Codigo, dto.Situacao.Rotulo).Should().Be(("AGUARDANDO_DOCUMENTO", "Aguardando documento"));
    }

    [Fact(DisplayName = "A instalação que aguarda o código mostra a situação Aguardando código como código e rótulo")]
    public void ToDto_AguardandoCodigo_MostraSituacaoComRotulo()
    {
        InstalacaoDto dto = InstalacaoMapping.ToDto(AguardandoCodigo());

        (dto.Situacao.Codigo, dto.Situacao.Rotulo).Should().Be(("AGUARDANDO_CODIGO", "Aguardando código"));
    }

    [Fact(DisplayName = "A tentativa com o código errado mostra o resultado Código errado como código e rótulo")]
    public void ToDto_TentativaComCodigoErrado_MostraResultadoComRotulo()
    {
        TentativaDeInstalacao tentativa = TentativaDeInstalacao.Registrar(
            Guid.NewGuid(), new Identidade(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001"), ResultadoDaTentativaDeInstalacao.CodigoErrado, DateTimeOffset.UtcNow);

        TentativaDeInstalacaoDto dto = InstalacaoMapping.ToDto(tentativa);

        (dto.Resultado.Codigo, dto.Resultado.Rotulo).Should().Be(("CODIGO_ERRADO", "Código errado"));
    }

    private static Instalacao Iniciada() => Instalacao.Iniciar(
        new Identidade(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001"),
        [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
        Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;

    private static Instalacao AguardandoCodigo()
    {
        Instalacao instalacao = Iniciada();
        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);
        instalacao.EnviarCodigo([1, 2, 3], DateTimeOffset.UtcNow);

        return instalacao;
    }
}
