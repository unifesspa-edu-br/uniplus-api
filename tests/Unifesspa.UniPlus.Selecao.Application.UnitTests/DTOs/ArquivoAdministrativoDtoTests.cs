namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

public sealed class ArquivoAdministrativoDtoTests
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    [Theory(DisplayName = "A classificação do arquivo sai no token canônico; a que não se resolve ou está fora do domínio vale como sensível")]
    [InlineData(ClassificacaoDeProtecao.Publico, "PUBLICO")]
    [InlineData(ClassificacaoDeProtecao.Pessoal, "PESSOAL")]
    [InlineData(ClassificacaoDeProtecao.Identificador, "IDENTIFICADOR")]
    [InlineData(ClassificacaoDeProtecao.Nenhuma, "SENSIVEL")]
    [InlineData((ClassificacaoDeProtecao)99, "SENSIVEL")]
    public void ClassificacaoProtecao_SaiNoToken(ClassificacaoDeProtecao classificacao, string esperado)
    {
        ArquivoAdministrativoDto arquivo = new(classificacao, null)
        {
            ArquivoId = Guid.CreateVersion7(),
            ExigenciaId = Guid.CreateVersion7(),
            NomeDoDocumento = "Laudo",
            TipoDeMidia = "application/pdf",
            Situacao = SituacaoDoArquivoDoCandidato.Verificado,
            ConfirmadoEm = DateTimeOffset.UtcNow,
        };

        arquivo.ClassificacaoProtecao.Should().Be(esperado);
        JsonSerializer.Serialize(arquivo, Opcoes)
            .Should().Contain($"\"classificacaoProtecao\":\"{esperado}\"");
    }
}
