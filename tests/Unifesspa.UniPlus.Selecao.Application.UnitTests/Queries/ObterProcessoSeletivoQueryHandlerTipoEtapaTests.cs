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

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A leitura do processo projeta a origem da nota no ENEM congelada na etapa, para o cliente
/// reconhecer a etapa de nota do ENEM sem consultar o cadastro atual.
/// </summary>
public sealed class ObterProcessoSeletivoQueryHandlerTipoEtapaTests
{
    [Fact(DisplayName = "A etapa projeta a nota de origem no ENEM congelada no tipo")]
    public async Task Handle_ProjetaANotaDeOrigemNoEnemCongelada()
    {
        ProcessoSeletivo processo = ProcessoComAsEtapas(
            EtapaDoTipo("Nota do ENEM", 1, "NOTA_ENEM", notaDeOrigemNoEnem: true),
            EtapaDoTipo("Redação", 2, "REDACAO", notaDeOrigemNoEnem: false));
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);

        dto!.Etapas.Select(etapa => (etapa.Identificacao.Nome, etapa.Identificacao.TipoEtapa.NotaDeOrigemNoEnem))
            .Should().Equal(("Nota do ENEM", true), ("Redação", false));
    }

    private static EtapaProcesso EtapaDoTipo(string nome, int ordem, string codigoDoTipo, bool notaDeOrigemNoEnem) =>
        EtapaDoProcesso(nome, CaraterEtapa.Classificatoria, ordem, TipoCongelado(Guid.CreateVersion7(), codigoDoTipo, nome, true, true, notaDeOrigemNoEnem), 1m);
}
