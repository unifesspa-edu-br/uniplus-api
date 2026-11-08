namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O comprovante da inscrição cancelada diz que não tem validade e quando ela foi cancelada, no fuso do processo.</summary>
public sealed class CancelamentoDoComprovanteTests
{
    // 08/11/2026 01:32:05 em UTC é 07/11/2026 22:32:05 em Belém: o dia civil muda com a zona.
    private static readonly DateTimeOffset CanceladaEm = new(2026, 11, 8, 1, 32, 5, TimeSpan.Zero);

    private static Inscricao Cancelada(string? motivo) => Inscricao.Reidratar(
        Guid.CreateVersion7(), Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond, "sub-1", "https://issuer", SituacaoInscricao.Cancelada,
        Guid.CreateVersion7(), null, null, "000123", CanceladaEm, "sub-1", motivo,
        new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(),
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), [], new Dictionary<string, DefinicaoDaResposta>(), 2, CanceladaEm, null);

    private static CancelamentoDoComprovante Novo(IResolvedorFusoInstitucional? fuso = null) => new(fuso ?? DivulgacaoDeTeste.Fuso());

    [Fact(DisplayName = "A inscrição cancelada traz o aviso com a data e a hora de Belém, o instante e o motivo")]
    public void Cancelada_TrazAvisoNoFusoDoProcesso()
    {
        Result<CancelamentoNoComprovanteDto?> resultado = Novo().Ler(Cancelada("Mudei de curso"));

        CancelamentoNoComprovanteDto cancelamento = resultado.Value!;
        cancelamento.Aviso.Should().Be("Documento sem validade: inscrição cancelada em 07/11/2026 às 22:32:05");
        cancelamento.CanceladaEm.Should().Be(CanceladaEm);
        cancelamento.Motivo.Should().Be("Mudei de curso");
    }

    [Fact(DisplayName = "A cancelada sem motivo traz o aviso e o motivo nulo, e o motivo nunca entra no texto da tarja")]
    public void CanceladaSemMotivo_MotivoNulo_AvisoSemMotivo()
    {
        CancelamentoNoComprovanteDto cancelamento = Novo().Ler(Cancelada(motivo: null)).Value!;

        cancelamento.Motivo.Should().BeNull();
        cancelamento.Aviso.Should().StartWith("Documento sem validade: inscrição cancelada em ");
        Novo().Ler(Cancelada("Mudei de curso")).Value!.Aviso.Should().NotContain("Mudei de curso");
    }

    [Fact(DisplayName = "A inscrição enviada não traz cancelamento e não resolve o fuso")]
    public void Enviada_SemCancelamento()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        Inscricao enviada = ComprovanteDeTeste.UmaInscricao();

        Result<CancelamentoNoComprovanteDto?> resultado = Novo(fuso).Ler(enviada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
        fuso.DidNotReceive().Resolver();
    }

    [Fact(DisplayName = "O fuso que a instalação não resolve recusa o comprovante em vez de sair com a data errada")]
    public void FusoNaoResolvido_Recusa()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Failure(new DomainError("ProcessoSeletivo.FusoInstitucionalNaoReconhecido", "sem tzdata")));

        Result<CancelamentoNoComprovanteDto?> resultado = Novo(fuso).Ler(Cancelada("a pedido do candidato"));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
    }
}
