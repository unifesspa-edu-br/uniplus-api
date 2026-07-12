namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do handler do snapshot vigente (ADR-0075/0076/0068/0103/0104): o instante
/// nunca é lido por dentro do seletor, a ausência de configuração é distinguida de
/// processo inexistente, e a versão eleita é projetada com a referência por VALOR ao
/// ato — sem consultar Publicações.
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
            instante: vigenteAPartirDe);

    [Fact(DisplayName = "Instante omitido usa o relógio injetado — o seletor jamais lê um relógio por dentro (ADR-0068)")]
    public async Task Handle_InstanteOmitido_PassaORelogioInjetadoAoSeletor()
    {
        Guid processoId = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await repository.Received(1).ObterVersaoVigenteAsync(processoId, Agora, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A versão vigente é projetada com a referência por VALOR ao ato — o par {id, hash} que ela já guarda")]
    public async Task Handle_VersaoVigente_ProjetaReferenciaPorValorAoAto()
    {
        Guid processoId = Guid.CreateVersion7();
        DateTimeOffset instante = Agora.AddDays(2);
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, instante, Arg.Any<CancellationToken>()).Returns(versao);

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, instante),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        SnapshotVigenteDto dto = resultado.Value!;
        dto.SnapshotPublicacaoId.Should().Be(versao.Id, "a referência forense durável é a da VERSÃO");
        dto.AtoId.Should().Be(versao.AtoCriadorId, "o id do ato é a metade-identificador da referência por valor (ADR-0061)");
        dto.HashEdital.Should().Be(versao.AtoCriadorHash, "e o hash do documento é a outra metade");
        dto.HashConfiguracao.Should().Be(versao.HashConfiguracao);
        dto.SchemaVersion.Should().Be(versao.SchemaVersion);
    }

    [Fact(DisplayName = "O contrato de leitura não republica atributo documental algum — o documento é de Publicações (ADR-0103/0105)")]
    public void SnapshotVigenteDto_NaoCarregaAtributoDocumental()
    {
        // O que a Seleção sabe do ato é o par {id, hash}, e mais nada. Tipo, número, data de
        // publicação e assinante pertencem ao ato, e o ato é de Publicações — republicá-los
        // aqui seria manter a posse do documento por outra porta, e obrigaria a Seleção a
        // saber o que um ato É. Se um destes campos reaparecer, é este teste que denuncia.
        IEnumerable<string> propriedades = typeof(SnapshotVigenteDto).GetProperties().Select(p => p.Name);

        propriedades.Should().NotContain(
            ["Natureza", "TipoAtoCodigo", "DataPublicacao", "Numero", "Assinante", "Orgao", "Serie"],
            "o contrato publica a REFERÊNCIA ao ato, não os atributos dele");
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

    [Fact(DisplayName = "A leitura do snapshot não espera o ato existir — o registro em Publicações é assíncrono (ADR-0108)")]
    public async Task Handle_VersaoVigente_NaoDependeDoRegistroDoAto()
    {
        Guid processoId = Guid.CreateVersion7();
        VersaoConfiguracao versao = NovaVersao(processoId, Agora);
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(versao);

        Result<SnapshotVigenteDto> resultado = await ObterSnapshotVigenteQueryHandler.Handle(
            new ObterSnapshotVigenteQuery(processoId, Instante: null),
            repository,
            new RelogioFixo(Agora),
            CancellationToken.None);

        // Entre o 204 da publicação e a drenagem do outbox, o ato ainda não existe em
        // Publicações. Se o snapshot dependesse dele para responder, essa janela viraria um
        // 404 (ou pior, um 500) num certame perfeitamente publicado. Ele não depende: o par
        // {id, hash} está na própria versão, e é UMA leitura só.
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.AtoId.Should().Be(versao.AtoCriadorId);
        await repository.Received(1).ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().ExisteAsync(processoId, Arg.Any<CancellationToken>());
    }

    /// <summary>Relógio fixo — o handler só precisa de um instante determinístico.</summary>
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
