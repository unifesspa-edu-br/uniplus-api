namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O prazo que o comprovante que vale mostra: o fim do período divulgado agora e se a correção ainda cabe. É informação acessória,
/// e a falta dele não derruba o comprovante.
/// </summary>
public sealed class PrazoDaCorrecaoDoComprovanteTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();

    private static Inscricao Enviada()
    {
        return Inscricao.Reidratar(Guid.CreateVersion7(), ProcessoId,
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Submetida, "000123", CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 1, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));
    }

    private static PrazoDaCorrecaoDoComprovante Prazo(DateTimeOffset agora, bool comVersaoDivulgada = true, IResolvedorFusoInstitucional? fuso = null)
    {
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        if (comVersaoDivulgada)
        {
            DivulgacaoDeTeste.ConfigurarDivulgada(processos, registro, versao, configuracao);
        }

        return new PrazoDaCorrecaoDoComprovante(
            DivulgacaoDeTeste.Certame(ProcessoId), processos, registro, fuso ?? DivulgacaoDeTeste.Fuso(), new RelogioFixo(agora));
    }

    [Fact(DisplayName = "O comprovante que não vale não traz prazo")]
    public async Task Ler_ComprovanteQueNaoVale_Nulo()
    {
        PrazoDaCorrecaoDto? prazo = await Prazo(DivulgacaoDeTeste.DentroDoPeriodo).LerAsync(Enviada(), vale: false, CancellationToken.None);

        prazo.Should().BeNull();
    }

    [Fact(DisplayName = "A inscrição cancelada não traz prazo da correção, ainda que o comprovante seja o que vale")]
    public async Task Ler_InscricaoCancelada_Nulo()
    {
        Inscricao cancelada = Inscricao.Reidratar(Guid.CreateVersion7(), ProcessoId,
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(DivulgacaoDeTeste.DentroDoPeriodo, "conta-1", null)),
            FormularioDaInscricao.Reidratar(Guid.CreateVersion7(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), []),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 2, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

        PrazoDaCorrecaoDto? prazo = await Prazo(DivulgacaoDeTeste.DentroDoPeriodo).LerAsync(cancelada, vale: true, CancellationToken.None);

        prazo.Should().BeNull();
    }

    [Fact(DisplayName = "Sem a versão divulgada legível, o prazo some e o comprovante segue")]
    public async Task Ler_SemVersaoDivulgada_Nulo()
    {
        PrazoDaCorrecaoDto? prazo = await Prazo(DivulgacaoDeTeste.DentroDoPeriodo, comVersaoDivulgada: false).LerAsync(Enviada(), vale: true, CancellationToken.None);

        prazo.Should().BeNull();
    }

    [Fact(DisplayName = "Sem o fuso institucional, o prazo some e o comprovante segue")]
    public async Task Ler_SemFuso_Nulo()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Failure(new DomainError("Fuso.Indisponivel", "sem fuso")));

        PrazoDaCorrecaoDto? prazo = await Prazo(DivulgacaoDeTeste.DentroDoPeriodo, fuso: fuso).LerAsync(Enviada(), vale: true, CancellationToken.None);

        prazo.Should().BeNull();
    }

    [Fact(DisplayName = "No período divulgado, o prazo é o fim dele e a correção está aberta; depois do fim, o prazo segue e a correção está fechada")]
    public async Task Ler_ComprovanteQueVale_DizSeACorrecaoEstaAberta()
    {
        Inscricao inscricao = Enviada();

        PrazoDaCorrecaoDto? aberto = await Prazo(DivulgacaoDeTeste.DentroDoPeriodo).LerAsync(inscricao, vale: true, CancellationToken.None);
        PrazoDaCorrecaoDto? fechado = await Prazo(DivulgacaoDeTeste.FimDoPeriodo.AddDays(1)).LerAsync(inscricao, vale: true, CancellationToken.None);

        aberto!.Aberto.Should().BeTrue();
        aberto.Fim.Should().Be(DivulgacaoDeTeste.FimDoPeriodo);
        fechado!.Aberto.Should().BeFalse();
        fechado.Fim.Should().Be(DivulgacaoDeTeste.FimDoPeriodo);
    }
}
