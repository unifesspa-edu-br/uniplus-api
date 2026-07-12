namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do handler do snapshot vigente (Story #803, ADR-0075/0076/0068/0104):
/// o instante nunca é lido por dentro do seletor, a ausência de configuração é
/// distinguida de processo inexistente, e a versão eleita é hidratada com os
/// dados documentais do ato que a criou.
/// </summary>
public sealed class ObterSnapshotVigenteQueryHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static VersaoConfiguracao NovaVersao(Guid processoId, DateTimeOffset vigenteAPartirDe) =>
        VersaoConfiguracao.Abrir(
            processoId,
            System.Text.Encoding.UTF8.GetBytes("""{"etapas":[]}"""),
            schemaVersion: "1.0",
            algoritmoHash: "canonical-json/sha256@v1",
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorHash: HashFixo,
            atorUsuarioSub: "user-sub-123",
            clock: new RelogioFixo(vigenteAPartirDe));

    [Fact(DisplayName = "Instante omitido usa o relógio injetado — o seletor jamais lê um relógio por dentro (ADR-0068)")]
    public async Task Handle_InstanteOmitido_PassaORelogioInjetadoAoSeletor()
    {
        Guid processoId = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        repository.ObterDadosDocumentaisDoAtoAsync(processoId, versao.AtoCriadorId, Arg.Any<CancellationToken>())
            .Returns(new DadosDocumentaisAto(Agora, nameof(NaturezaEdital.Abertura)));

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await repository.Received(1).ObterVersaoVigenteAsync(processoId, Agora, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão vigente é projetada com os dados documentais do ato que a criou — contrato de leitura inalterado")]
    public async Task Handle_VersaoVigente_ProjetaSnapshotComDadosDoAto()
    {
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = Agora.AddDays(2);
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);
        DateTimeOffset dataDocumental = Agora.AddDays(-10);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, instante, Arg.Any<CancellationToken>()).Returns(versao);
        repository.ObterDadosDocumentaisDoAtoAsync(processoId, versao.AtoCriadorId, Arg.Any<CancellationToken>())
            .Returns(new DadosDocumentaisAto(dataDocumental, nameof(NaturezaEdital.Retificacao)));

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, instante),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        SnapshotVigenteDto dto = resultado.Value!;
        dto.SnapshotPublicacaoId.Should().Be(versao.Id, "a referência forense durável é a da VERSÃO");
        dto.HashConfiguracao.Should().Be(versao.HashConfiguracao);
        dto.HashEdital.Should().Be(versao.AtoCriadorHash);
        dto.SchemaVersion.Should().Be(versao.SchemaVersion);
        dto.Natureza.Should().Be(nameof(NaturezaEdital.Retificacao));
        dto.DataPublicacao.Should().Be(
            dataDocumental,
            "a data publicada é a DOCUMENTAL — que pode preceder a vigência, e não ordena nada");
    }

    [Fact(DisplayName = "Sem versão vigente em processo existente retorna 422 Snapshot.VigenteAusente — a ausência aflora (ADR-0076)")]
    public async Task Handle_SemVersaoVigente_ProcessoExiste_RetornaVigenteAusente()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((VersaoConfiguracao?)null);
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }

    [Fact(DisplayName = "Sem versão vigente em processo inexistente retorna 404 — nunca um erro genérico")]
    public async Task Handle_SemVersaoVigente_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((VersaoConfiguracao?)null);
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(false);

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Versão vigente cujo ato não existe é corrupção, não ausência — falha alto, sem mascarar como 422")]
    public async Task Handle_VersaoVigenteSemAto_LancaEstadoInconsistente()
    {
        Guid processoId = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(versao);
        repository.ObterDadosDocumentaisDoAtoAsync(processoId, versao.AtoCriadorId, Arg.Any<CancellationToken>())
            .Returns((DadosDocumentaisAto?)null);

        Func<Task> acao = async () => await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        // 422 diria ao cliente que o certame não tem configuração vigente — e tem;
        // o que falta é o documento. A ausência que a ADR-0076 manda aflorar é a de
        // configuração, não a de evidência corrompida.
        await acao.Should().ThrowAsync<InvalidOperationException>();
        await repository.DidNotReceive().ExisteAsync(processoId, Arg.Any<CancellationToken>());
    }

    /// <summary>Relógio fixo — o handler só precisa de um instante determinístico.</summary>
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
