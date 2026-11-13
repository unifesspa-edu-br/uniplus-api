namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DeclaracaoDasBancasDaEtapaTests
{
    private static readonly Guid TipoDaProva = Guid.CreateVersion7();
    private static readonly TipoBancaView TipoExaminadora = new(Guid.CreateVersion7(), "BANCA_EXAMINADORA", "Banca examinadora", null, null);
    private static readonly TipoBancaView TipoDeRecursos = new(Guid.CreateVersion7(), "BANCA_ANALISE_RECURSOS", "Banca de análise de recursos", null, null);
    private static readonly string[] CamposDaPrimeiraEDaTerceiraBanca =
    [
        "bancas[0].regraDeDeliberacao.integrantes",
        "bancas[0].regraDeDeliberacao.criterio",
        "bancas[0].regraDeDeliberacao.encerramento",
        "bancas[2].regraDeDeliberacao.integrantes",
        "bancas[2].regraDeDeliberacao.criterio",
        "bancas[2].regraDeDeliberacao.encerramento",
    ];

    [Fact(DisplayName = "A banca da etapa congela o código e o nome do tipo no cadastro, com a regra declarada")]
    public async Task Declarar_ComRegra_CongelaOTipoEARegra()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoExaminadora, RegrasDeDeliberacaoDeTeste.Valida())), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Should().ContainSingle().Subject;
        banca.Codigo.Should().Be("BANCA_EXAMINADORA");
        banca.Nome.Should().Be("Banca examinadora");
        banca.RegraDeDeliberacao!.Integrantes.Should().Be(3);
    }

    [Fact(DisplayName = "A banca sem regra é rascunho e a declaração a aceita")]
    public async Task Declarar_SemRegra_AceitaORascunho()
    {
        EtapaProcesso etapa = NovaEtapa();

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoExaminadora, null)), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        etapa.Bancas.Single().RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "As violações da regra de todas as bancas saem juntas, cada uma sob o caminho da sua banca")]
    public async Task Declarar_ComRegrasInvalidas_AcumulaAsViolacoesDeTodasAsBancas()
    {
        EtapaProcesso etapa = NovaEtapa();
        EtapaProcessoInput entrada = Entrada(
            Banca(TipoExaminadora, RegrasDeDeliberacaoDeTeste.ComTresViolacoes()),
            Banca(TipoDeRecursos, RegrasDeDeliberacaoDeTeste.Valida()),
            Banca(TipoExaminadora, RegrasDeDeliberacaoDeTeste.ComTresViolacoes()));

        Result resultado = await Declaracao().DeclararAsync(etapa, entrada, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(CamposDaPrimeiraEDaTerceiraBanca);
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo de banca ausente do cadastro interrompe a declaração, sem campo e sem alterar a etapa")]
    public async Task Declarar_ComTipoAusenteDoCadastro_InterrompeSemAlterarAEtapa()
    {
        EtapaProcesso etapa = NovaEtapa();
        BancaDaEtapaInput desconhecida = new(Guid.CreateVersion7(), RegrasDeDeliberacaoDeTeste.Valida());

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(desconhecida), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("EtapaProcesso.TipoBancaNaoEncontradoOuInativo");
        etapa.Bancas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Redeclarar o mesmo tipo reaproveita a linha da banca e atualiza a regra")]
    public async Task Declarar_ReaproveitandoABanca_AtualizaARegraSemTrocarALinha()
    {
        EtapaProcesso etapa = NovaEtapa();
        await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoExaminadora, null)), CancellationToken.None);
        Guid linha = etapa.Bancas.Single().Id;

        Result resultado = await Declaracao().DeclararAsync(etapa, Entrada(Banca(TipoExaminadora, RegrasDeDeliberacaoDeTeste.Valida())), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Should().ContainSingle().Subject;
        banca.Id.Should().Be(linha);
        banca.RegraDeDeliberacao!.Quorum.Should().Be(2);
    }

    private static DeclaracaoDasBancasDaEtapa Declaracao() => new(new LeituraMemoizada<Guid, TipoBancaView>(
        (tipoId, _) => Task.FromResult<TipoBancaView?>(new[] { TipoExaminadora, TipoDeRecursos }.FirstOrDefault(tipo => tipo.Id == tipoId))));

    private static BancaDaEtapaInput Banca(TipoBancaView tipo, RegraDeDeliberacaoInput? regra) => new(tipo.Id, regra);

    private static EtapaProcessoInput Entrada(params BancaDaEtapaInput[] bancas) => new(
        "Prova oral", CaraterEtapa.Classificatoria, TipoDaProva, 1m, null, 1, Produtos: [], Bancas: bancas, Recursos: []);

    private static EtapaProcesso NovaEtapa() => EtapaProcesso.Criar(
        "Prova oral", CaraterEtapa.Classificatoria,
        TipoEtapaSnapshot.Criar(TipoDaProva, "PROVA_ORAL", "Prova oral", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: 1m, ordem: 1).Value!;
}
