namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura do processo projeta a origem da nota no ENEM congelada na etapa, para o cliente
/// reconhecer a etapa de nota do ENEM sem consultar o cadastro atual.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerTipoEtapaTests
{
    [Fact(DisplayName = "A etapa projeta a nota de origem no ENEM congelada no tipo")]
    public async Task Handle_ProjetaANotaDeOrigemNoEnemCongelada()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Query Tipo de Etapa", TiposDeProcessoDeTeste.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());
        EtapaProcesso doEnem = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Nota do ENEM", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        EtapaProcesso redacao = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 2, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        processo.DefinirEtapas([doEnem, redacao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto!.Etapas.Select(etapa => (etapa.Identificacao.Nome, etapa.Identificacao.TipoEtapa.NotaDeOrigemNoEnem))
            .Should().Equal(("Nota do ENEM", true), ("Redação", false));
    }
}
