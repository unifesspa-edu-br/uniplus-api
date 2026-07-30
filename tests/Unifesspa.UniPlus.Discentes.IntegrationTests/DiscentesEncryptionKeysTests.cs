namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Cryptography;

public sealed class DiscentesEncryptionKeysTests
{
    [Fact]
    public void IdentificadoresPessoais_UsaChaveExclusivaDoModulo()
    {
        DiscentesEncryptionKeys.IdentificadoresPessoais
            .Should().Be("uniplus-discentes-identificadores-aesgcm");
    }
}
