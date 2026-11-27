namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>O par {id, hash} da versão do objeto declarado no pedido: completo ou ausente.</summary>
public sealed class ParDaVersaoDoObjetoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact(DisplayName = "Par ausente é aceito sem referência")]
    public void DoPar_Ausente_AceitoSemReferencia()
    {
        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(null, null);

        parte.EstaAceita.Should().BeTrue();
        parte.Valor.Should().BeNull();
    }

    [Fact(DisplayName = "Par completo é aceito com a referência")]
    public void DoPar_Completo_AceitoComReferencia()
    {
        Guid id = Guid.CreateVersion7();

        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(id, HashValido);

        parte.Valor!.Id.Should().Be(id);
        parte.Valor.Hash.Should().Be(HashValido);
    }

    [Fact(DisplayName = "Só o identificador aponta o hash que falta")]
    public void DoPar_SoOId_ApontaOHash()
    {
        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(Guid.CreateVersion7(), null);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("versaoDoObjetoHash", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta));
    }

    [Fact(DisplayName = "Só o hash aponta o identificador que falta")]
    public void DoPar_SoOHash_ApontaOId()
    {
        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(null, HashValido);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("versaoDoObjetoId", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta));
    }

    [Fact(DisplayName = "Identificador vazio e hash inválido são recusados juntos, o identificador primeiro")]
    public void DoPar_IdVazioEHashInvalido_AcumulaOsDois()
    {
        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(Guid.Empty, "abc");

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(
            ("versaoDoObjetoId", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta),
            ("versaoDoObjetoHash", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta));
    }

    [Fact(DisplayName = "Hash vazio sem identificador é recusado nos dois lados")]
    public void DoPar_HashVazioSemId_RecusaNosDoisLados()
    {
        ParteConferida<ReferenciaVersaoDoObjeto> parte = ReferenciaVersaoDoObjeto.DoPar(null, string.Empty);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(
            ("versaoDoObjetoId", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta),
            ("versaoDoObjetoHash", AtoNormativoErrorCodes.VersaoDoObjetoIncompleta));
    }
}
