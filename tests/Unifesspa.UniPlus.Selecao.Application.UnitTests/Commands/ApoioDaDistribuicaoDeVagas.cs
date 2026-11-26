namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

        internal Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> DefinirAsync(DefinirDistribuicaoVagasCommand command) =>
            DefinirDistribuicaoVagasCommandHandler.Handle(command, Repository, Resolvedor, CadastrosVivos.CatalogoDeFatos(), UnitOfWork, CancellationToken.None);

        internal void NenhumCadastroConsultado()
        {
            RegraCatalogoReader.ReceivedCalls().Should().BeEmpty();
            OfertaCursoReader.ReceivedCalls().Should().BeEmpty();
            ModalidadeReader.ReceivedCalls().Should().BeEmpty();
            ReferenciaReservaDemograficaReader.ReceivedCalls().Should().BeEmpty();
        }

        internal Mocks ComOferta(Guid ofertaCursoId, GrupoAreaEnemView? grupoAreaEnem = null)
        {
            OfertaCursoReader.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(NovaOferta(ofertaCursoId, grupoAreaEnem));
            return this;
        }

        internal Mocks ComRegraInstitucional()
        {
            RegraCatalogoReader.ObterAsync(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", Arg.Any<CancellationToken>())
                .Returns(RegraDistribuicao(RegraDeDistribuicaoDeVagas.Institucional.Codigo()));
            return this;
        }

        internal Mocks ComModalidadeAmpla(Guid modalidadeId)
        {
            ModalidadeReader.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(NovaModalidadeAmpla(modalidadeId));
            return this;
        }
    }

    internal static ProcessoSeletivo NovoProcesso(TipoProcessoSnapshot? tipo = null) =>
        ProcessoSeletivo.Criar(
            "Processo 2026", tipo ?? TiposDeProcessoDeTeste.IndigenaEQuilombola, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    /// <summary>Distribuição institucional de uma oferta, com a ampla concorrência como única modalidade.</summary>
    internal static ConfiguracaoDistribuicaoVagasInput DistribuicaoInstitucional(Guid ofertaCursoId, Guid modalidadeId, int vagas) =>
        new(ofertaCursoId, vagas, 1m, RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", null, null, null,
            [modalidadeId], [new QuantidadeVagaInput(modalidadeId, vagas)]);

    /// <summary>Distribuição sem quadro de vagas, para os testes que recusam antes de conferi-lo.</summary>
    internal static ConfiguracaoDistribuicaoVagasInput DistribuicaoSemQuadro(
        Guid ofertaCursoId, string regraCodigo, IReadOnlyList<Guid> modalidadeIds, decimal fator = 1m, Guid? referenciaId = null) =>
        new(ofertaCursoId, 50, fator, regraCodigo, "v1", null, null, referenciaId, modalidadeIds, []);

    internal static DefinirDistribuicaoVagasCommand ComandoDaDistribuicao(ProcessoSeletivo processo, params ConfiguracaoDistribuicaoVagasInput[] distribuicoes) =>
        new(processo.Id, distribuicoes);

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
        null, "SEGUE_ORDEM_DE_REMANEJAMENTO", null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012 art. 3º");
}
