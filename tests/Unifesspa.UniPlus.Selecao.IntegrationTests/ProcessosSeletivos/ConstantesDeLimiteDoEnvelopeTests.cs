namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

// Confronta só o limite do nome do tipo de ato com a coluna. Não detecta uma constante nova de
// LimitesDoEnvelope que fique sem confronto: essa conferência automática ainda não existe.
public sealed class ConstantesDeLimiteDoEnvelopeTests
{
    [Fact]
    public void NomeDoTipoDeAto_LimiteDoEnvelope_IgualAoDaColuna()
    {
        using UniPlusDbContext contexto = ModeloEmMemoria.Contexto();
        int? limiteDaColuna = contexto.Model.FindEntityType(typeof(TipoAtoPublicado))!
            .FindProperty(nameof(TipoAtoPublicado.Nome))!.GetMaxLength();

        LimitesDoEnvelope.TipoAtoNome.Should().Be(limiteDaColuna);
    }
}
