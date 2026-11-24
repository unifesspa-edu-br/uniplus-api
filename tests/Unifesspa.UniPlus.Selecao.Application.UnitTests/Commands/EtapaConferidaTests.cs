namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class EtapaConferidaTests
{
    private static readonly Guid TipoOrigemId = Guid.CreateVersion7();

    private static EtapaProcesso EtapaExistente(bool exigeCorrecaoIndependente) => EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
        new IdentificacaoDaEtapa(
            Nome: "Prova", Carater: CaraterEtapa.Classificatoria,
            TipoEtapa: TipoEtapaSnapshot.Criar(TipoOrigemId, "PROVA", "Prova", true, true, false, exigeCorrecaoIndependente).Value!,
            Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(
            Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null,
            FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcessoInput Declarada(EtapaProcesso existente, CaraterEtapa carater) => new(
        existente.Nome, carater.Codigo(), TipoOrigemId, 1m, null, 1, Id: existente.Id, Produtos: [], Bancas: [], Recursos: []);

    private static TipoEtapaView TipoLido(bool exigeCorrecaoIndependente) =>
        TipoEtapaViewDeTeste.Com(TipoOrigemId, "PROVA").Chamado("Prova").ComCorrecaoIndependente(exigeCorrecaoIndependente).Construir();

    [Fact(DisplayName = "A etapa de vínculo igual e caráter trocado exige correção independente conforme o tipo relido")]
    public void ExigeCorrecaoIndependente_VinculoIgualComTipoRelido_UsaOTipoRelido()
    {
        EtapaProcesso existente = EtapaExistente(exigeCorrecaoIndependente: false);

        EtapaConferida conferida = new(Declarada(existente, CaraterEtapa.Eliminatoria), existente, TipoLido(exigeCorrecaoIndependente: true));

        conferida.VinculoInalterado.Should().BeTrue();
        conferida.ExigeCorrecaoIndependente.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa que mantém vínculo e caráter mantém o valor do snapshot congelado")]
    public void ExigeCorrecaoIndependente_SemTipoRelido_UsaOSnapshot()
    {
        EtapaProcesso existente = EtapaExistente(exigeCorrecaoIndependente: false);

        EtapaConferida conferida = new(Declarada(existente, CaraterEtapa.Classificatoria), existente);

        conferida.ExigeCorrecaoIndependente.Should().BeFalse();
    }
}
