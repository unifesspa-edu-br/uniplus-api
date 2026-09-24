namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
            "PS Query Tipo de Etapa", TipoProcesso.PSIQ, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        EtapaProcesso doEnem = EtapaProcesso.Criar("Nota do ENEM", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "NOTA_ENEM", "Nota do ENEM", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!, 1m, ordem: 1).Value!;
        EtapaProcesso redacao = EtapaProcesso.Criar("Redação", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, 1m, ordem: 2).Value!;
        processo.DefinirEtapas([doEnem, redacao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto!.Etapas.Select(etapa => (etapa.Nome, etapa.TipoEtapa.NotaDeOrigemNoEnem))
            .Should().Equal(("Nota do ENEM", true), ("Redação", false));
    }
}
