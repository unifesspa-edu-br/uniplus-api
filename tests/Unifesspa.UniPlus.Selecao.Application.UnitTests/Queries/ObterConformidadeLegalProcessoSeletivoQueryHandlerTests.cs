namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #853, CA-16 (fonte única): a consulta pública usa exatamente a mesma dupla de
/// chamadas do gate que bloqueia a transição —
/// <c>ObterVigentesParaTipoProcessoAsync</c> + <c>AvaliadorConformidadeLegal.Avaliar</c>
/// (ver <see cref="ObterConformidadeLegalProcessoSeletivoQueryHandler"/> e
/// <c>ConferenciaDeConformidadeLegal</c>, interno à Application). Este teste prova que, para
/// o MESMO processo/regras/data de corte, a consulta produz item a item o mesmo veredicto
/// que uma chamada direta ao avaliador (Domain, público) — nunca duas leituras em paralelo.
/// </summary>
public sealed class ObterConformidadeLegalProcessoSeletivoQueryHandlerTests
{
    private static ObrigatoriedadeLegal NovaRegra(string regraCodigo, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: "Regra de teste",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

    [Fact(DisplayName = "CA-16: o veredicto da consulta pública bate, item a item, com o do avaliador que também alimenta o gate")]
    public async Task Consulta_ComOMesmoProcessoERegras_BateComOAvaliadorDoGate()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)],
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ObrigatoriedadeLegal regraAprovada = NovaRegra("CONSULTA-APROVA", new EtapaObrigatoria("Prova Objetiva"));
        ObrigatoriedadeLegal regraReprovada = NovaRegra("CONSULTA-REPROVA", new EtapaObrigatoria("Entrevista"));
        DateOnly dataDeCorte = new(2026, 1, 1);

        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();
        obrigatoriedadeLegalRepository.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), dataDeCorte, Arg.Any<CancellationToken>())
            .Returns([regraAprovada, regraReprovada]);

        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>())
            .Returns(processo);

        ConformidadeLegalProcessoSeletivoDto? dto = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(processo.Id, dataDeCorte),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Regras.Should().HaveCount(2);

        RegraAvaliadaDto reprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-REPROVA");
        reprovadaNaConsulta.Aprovada.Should().BeFalse();
        reprovadaNaConsulta.Motivo.Should().Contain("Entrevista",
            "a consulta pública tem de expor o motivo nomeado (CA-02), não projetar Motivo: null");
        RegraAvaliadaDto aprovadaNaConsulta = dto.Regras.Single(r => r.RegraCodigo == "CONSULTA-APROVA");
        aprovadaNaConsulta.Aprovada.Should().BeTrue();
        aprovadaNaConsulta.Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");

        // Fonte única: (RegraId, Aprovada, Motivo) da consulta bate, item a item, com o que o
        // MESMO avaliador (Domain, chamado pelo gate na Application) produz para o mesmo
        // processo/regras/data — a consulta não tem lógica própria de decisão.
        Dictionary<Guid, (bool Aprovada, string? Motivo)> avaliacaoDireta = AvaliadorConformidadeLegal
            .Avaliar(processo, processo.Tipo.ToString(), [regraAprovada, regraReprovada])
            .Regras.ToDictionary(r => r.RegraId, r => (r.Aprovada, r.Motivo));

        foreach (RegraAvaliadaDto regraDaConsulta in dto.Regras)
        {
            avaliacaoDireta.Should().ContainKey(regraDaConsulta.RegraId);
            avaliacaoDireta[regraDaConsulta.RegraId].Aprovada.Should().Be(regraDaConsulta.Aprovada);
            avaliacaoDireta[regraDaConsulta.RegraId].Motivo.Should().Be(regraDaConsulta.Motivo);
        }
    }

    [Fact(DisplayName = "Processo inexistente devolve null, sem consultar o catálogo de obrigatoriedades")]
    public async Task ProcessoInexistente_DevolveNull()
    {
        IProcessoSeletivoRepository processoSeletivoRepository = Substitute.For<IProcessoSeletivoRepository>();
        processoSeletivoRepository.ObterComConfiguracaoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ProcessoSeletivo?)null);
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = Substitute.For<IObrigatoriedadeLegalRepository>();

        ConformidadeLegalProcessoSeletivoDto? dto = await ObterConformidadeLegalProcessoSeletivoQueryHandler.Handle(
            new ObterConformidadeLegalProcessoSeletivoQuery(Guid.CreateVersion7(), new DateOnly(2026, 1, 1)),
            processoSeletivoRepository,
            obrigatoriedadeLegalRepository,
            CancellationToken.None);

        dto.Should().BeNull();
        _ = await obrigatoriedadeLegalRepository.DidNotReceive().ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
