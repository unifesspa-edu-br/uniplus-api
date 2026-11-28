namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A correção recusada (motivo sem ato corrigido, ou o contrário) sai antes de qualquer leitura, mas na mesma resposta
/// da data e dos documentos ausentes do ato avulso, que não dependem dela.
/// </summary>
public sealed class RecusaDaCorrecaoDoAtoTests
{
    private static PublicarAtoAvulsoDoProcessoCommand Avulso() =>
        new(Guid.CreateVersion7(), "AVISO", "5/2026", new DateOnly(2026, 12, 1), "Diretor do CEPS", Guid.CreateVersion7());

    private static string[] Campos(Result<AtoPublicadoDto>? recusa) => [.. recusa!.Errors.Select(static e => e.Field!)];

    [Fact(DisplayName = "A correção válida não é recusada aqui")]
    public void CorrecaoValida_NaoRecusa() =>
        RecusaDaCorrecaoDoAto.De(Avulso() with { AtoCorrigidoId = Guid.CreateVersion7(), MotivoDaCorrecao = "Data errada" }).Should().BeNull();

    [Fact(DisplayName = "O motivo sem ato corrigido, com a data e o documento principal ausentes, acumula as três recusas")]
    public void MotivoSemAto_ComDataEDocumentoAusentes_AcumulaAsTres()
    {
        Result<AtoPublicadoDto>? recusa = RecusaDaCorrecaoDoAto.De(
            Avulso() with { MotivoDaCorrecao = "Data errada", DataDoAto = null, DocumentoPrincipalId = null });

        Campos(recusa).Should().Equal("atoCorrigidoId", "dataDoAto", "documentoPrincipalId");
        recusa!.Errors.Skip(1).Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "O ato corrigido sem motivo acumula com o anexo sem documento")]
    public void AtoSemMotivo_ComAnexoSemDocumento_AcumulaAsDuas() =>
        Campos(RecusaDaCorrecaoDoAto.De(Avulso() with { AtoCorrigidoId = Guid.CreateVersion7(), Anexos = [new(null, "Quadro")] }))
            .Should().Equal("motivoDaCorrecao", "anexos[0].documentoId");

    [Fact(DisplayName = "A correção recusada acumula com a quantidade de anexos acima do limite")]
    public void CorrecaoRecusada_ComAnexosAcimaDoLimite_AcumulaAQuantidade()
    {
        AnexoSolicitadoDoAto[] anexos = [.. Enumerable.Range(0, 21).Select(static _ => new AnexoSolicitadoDoAto(Guid.CreateVersion7(), "Quadro"))];

        Result<AtoPublicadoDto>? recusa = RecusaDaCorrecaoDoAto.De(Avulso() with { AtoCorrigidoId = Guid.CreateVersion7(), Anexos = anexos });

        recusa!.Errors.Select(static e => e.Field).Should().Equal("motivoDaCorrecao", null);
        recusa!.Errors[^1].Error.Code.Should().Be(AtoDoProcessoErrorCodes.QuantidadeDeAnexosExcedida);
    }

    [Fact(DisplayName = "A correção recusada sem outra ausência devolve só a recusa dela")]
    public void CorrecaoRecusada_SemOutraAusencia_DevolveSoElaMesma() =>
        Campos(RecusaDaCorrecaoDoAto.De(Avulso() with { AtoCorrigidoId = Guid.CreateVersion7() })).Should().Equal("motivoDaCorrecao");
}
