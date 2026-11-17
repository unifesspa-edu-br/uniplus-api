namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDaDistribuicaoDeVagas
{
    internal static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    internal static RegraCatalogo RegraDistribuicao(string codigo) => RegraCatalogo.Criar(
        codigo, "v1", TipoRegra.RegraDistribuicaoVagas, Json("{}"), Json("[]"), "base legal");

    internal static RegraCatalogo RegraDistribuicaoComRol(string codigo, params string[] modalidadesAdmitidas) => RegraCatalogo.Criar(
        codigo, "v1", TipoRegra.RegraDistribuicaoVagas,
        Json($"{{\"modalidades_admitidas\":[{string.Join(",", modalidadesAdmitidas.Select(static m => $"\"{m}\""))}]}}"),
        Json("[]"), "base legal");

    internal sealed record Mocks(
        IProcessoSeletivoRepository Repository,
        IRegraCatalogoReader RegraCatalogoReader,
        IOfertaCursoReader OfertaCursoReader,
        IModalidadeReader ModalidadeReader,
        IReferenciaReservaDemograficaReader ReferenciaReservaDemograficaReader,
        IUnitOfWork UnitOfWork)
    {
        public ConfiguracaoDistribuicaoVagasResolver Resolvedor =>
            new(RegraCatalogoReader, OfertaCursoReader, ModalidadeReader, ReferenciaReservaDemograficaReader);
    }

    internal static Mocks NovosMocks(ProcessoSeletivo? processo, Guid processoId)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processoId, Arg.Any<CancellationToken>()).Returns(processo);
        return new Mocks(
            repository,
            Substitute.For<IRegraCatalogoReader>(),
            Substitute.For<IOfertaCursoReader>(),
            Substitute.For<IModalidadeReader>(),
            Substitute.For<IReferenciaReservaDemograficaReader>(),
            Substitute.For<IUnitOfWork>());
    }

    internal static OfertaCursoView NovaOferta(Guid id, GrupoAreaEnemView? grupoAreaEnem = null) => new(
        id, "MEDICINA_MARABA", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
        "REGULAR", ["MATUTINO"], null, null, 100, null, null, grupoAreaEnem, "Medicina", "Bacharelado");

    internal static ModalidadeView NovaModalidadeAmpla(Guid id) => new(
        id, "AC", "Ampla concorrência", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS",
        null, null, null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012");

    internal static ModalidadeView NovaModalidadeCotaReservada(Guid id, string codigo) => new(
        id, codigo, null, "COTA_RESERVADA", "DENTRO_DA_RESERVA_LEGAL",
        null, "SEGUE_CASCATA", null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012 art. 3º");
}
