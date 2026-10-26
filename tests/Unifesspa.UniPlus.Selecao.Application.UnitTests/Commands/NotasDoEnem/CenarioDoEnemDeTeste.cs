namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.NotasDoEnem;

using NSubstitute;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um certame divulgado com uma etapa de nota do ENEM, uma etapa sem ela e as edições declaradas, lido
/// pela versão divulgada. O processo vivo nunca é consultado: se um handler o consultasse, o teste
/// falharia.
/// </summary>
internal sealed class CenarioDoEnemDeTeste
{
    public Guid ProcessoId { get; } = Guid.CreateVersion7();

    public EtapaProcesso EtapaDoEnem { get; }

    public EtapaProcesso EtapaSemEnem { get; }

    public ICertameDivulgadoRepository Certame { get; }

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();

    public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();

    public CenarioDoEnemDeTeste(params string[] edicoesDivulgadas)
    {
        EtapaDoEnem = Etapa("Nota do ENEM", "NOTA_ENEM", notaDeOrigemNoEnem: true);
        EtapaSemEnem = Etapa("Prova objetiva", "PROVA_OBJETIVA", notaDeOrigemNoEnem: false);

        (_, GrafoConfiguracao modelo) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        OpcaoDeclaradaFato[] opcoes =
        [
            .. edicoesDivulgadas.Select((e, i) => OpcaoDeclaradaFato.Criar(EdicaoDoEnem.Fato, e, $"ENEM {e}", i + 1).Value!),
        ];
        GrafoConfiguracao grafo = new(
            [EtapaDoEnem, EtapaSemEnem], modelo.OfertaAtendimento, [.. modelo.DistribuicaoVagas], modelo.BonusRegional,
            [.. modelo.CriteriosDesempate], modelo.Classificacao, [.. modelo.CronogramaFases], [.. modelo.DocumentosExigidos], [], null,
            fatosColetados: [.. modelo.FatosColetados], formularios: [.. modelo.Formularios], gruposColetados: [.. modelo.GruposColetados],
            opcoesDeclaradas: opcoes, numeroDeOpcoesDeCurso: 1);

        (VersaoConfiguracao versao, _) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        Certame = DivulgacaoDeTeste.Certame(ProcessoId);
        DivulgacaoDeTeste.ConfigurarDivulgada(Processos, Codecs, versao, grafo);
    }

    /// <summary>O processo vivo não pode ser lido: o escopo é o da versão divulgada.</summary>
    public void ProcessoVivoNaoFoiConsultado() =>
        Processos.DidNotReceiveWithAnyArgs().ObterComConfiguracaoAsync(default, default);

    private static EtapaProcesso Etapa(string nome, string tipo, bool notaDeOrigemNoEnem) =>
        EtapaProcesso.Criar(
            nome,
            CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(
                CadastrosVivos.IdentidadeDe(tipo), tipo, nome, admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDeOrigemNoEnem).Value!,
            peso: 1m,
            ordem: 1).Value!;
}
