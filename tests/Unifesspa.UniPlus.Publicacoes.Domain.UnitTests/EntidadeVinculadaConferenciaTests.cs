namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>A entidade vinculada: o rótulo na forma canônica e o identificador não vazio.</summary>
public sealed class EntidadeVinculadaConferenciaTests
{
    [Fact(DisplayName = "Entidade com rótulo canônico e identificador é aceita como chegou")]
    public void Conferir_Valida_Aceita()
    {
        EntidadeVinculada entidade = new("PROCESSO_SELETIVO", Guid.CreateVersion7());

        entidade.Conferir().Valor.Should().Be(entidade);
    }

    [Theory(DisplayName = "Rótulo nulo, vazio ou em branco é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Conferir_TipoVazio_Recusa(string? tipo)
    {
        ParteConferida<EntidadeVinculada> parte = new EntidadeVinculada(tipo!, Guid.CreateVersion7()).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido));
        parte.Recusas[0].Error.Message.Should().Be("O tipo da entidade vinculada é obrigatório.");
    }

    [Theory(DisplayName = "Rótulo fora da forma canônica é recusado com o mesmo código e outra mensagem, sem repetir o valor")]
    [InlineData("processo_seletivo")]
    [InlineData("PROCESSO-SELETIVO")]
    [InlineData(" PROCESSO")]
    public void Conferir_TipoForaDaForma_RecusaSemRepetirOValor(string tipo)
    {
        ParteConferida<EntidadeVinculada> parte = new EntidadeVinculada(tipo, Guid.CreateVersion7()).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido));
        parte.Recusas[0].Error.Message.Should().NotBe("O tipo da entidade vinculada é obrigatório.")
            .And.NotContain(tipo);
    }

    [Fact(DisplayName = "Rótulo com 61 caracteres é recusado e com 60 é aceito")]
    public void Conferir_TipoNoLimite_RecusaAcimaDe60()
    {
        new EntidadeVinculada(new string('A', 61), Guid.CreateVersion7()).Conferir().EstaAceita.Should().BeFalse();
        new EntidadeVinculada(new string('A', 60), Guid.CreateVersion7()).Conferir().EstaAceita.Should().BeTrue();
    }

    [Fact(DisplayName = "Identificador vazio é recusado")]
    public void Conferir_IdVazio_Recusa()
    {
        ParteConferida<EntidadeVinculada> parte = new EntidadeVinculada("PROCESSO", Guid.Empty).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade));
    }

    [Fact(DisplayName = "Rótulo inválido e identificador vazio são recusados juntos, o rótulo primeiro")]
    public void Conferir_TipoEIdInvalidos_AcumulaNaOrdem()
    {
        ParteConferida<EntidadeVinculada> parte = new EntidadeVinculada("x", Guid.Empty).Conferir();

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(
            ("entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido),
            ("entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade));
    }

    [Fact(DisplayName = "Conferida lança ArgumentException com o campo da primeira recusa")]
    public void Conferida_Invalida_LancaComOCampo()
    {
        Action acao = () => new EntidadeVinculada("PROCESSO", Guid.Empty).Conferida();

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("entidadeId");
    }
}
