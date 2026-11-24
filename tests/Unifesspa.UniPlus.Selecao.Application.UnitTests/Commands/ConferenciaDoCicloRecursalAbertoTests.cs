namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A conferência do ciclo recursal aberto lê os atos publicados do processo e a versão publicada vigente e
/// entrega ao domínio os itens cuja primeira publicação existe; a regra em si é testada no domínio.
/// </summary>
public sealed class ConferenciaDoCicloRecursalAbertoTests
{
    private const string Materia = "RESULTADO_PROVA";
    private static readonly DateTimeOffset Instante = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O preliminar que o registro de atos mostra publicado e cujo definitivo não saiu recusa a retirada do par, nomeando a matéria")]
    public async Task PreliminarPublicadoNosAtos_ProcessoSemOPar_RecusaPorMateria()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();

        Result resultado = await ConferirAsync(cenario);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("/cronograma/fases");
        recusa.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
    }

    [Fact(DisplayName = "Sem item do edital publicado nos atos a conferência aceita sem ler a versão publicada")]
    public async Task SemItemPublicadoNosAtos_AceitaSemReidratarAVersao()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        cenario.Servicos.Atos.ListarDoProcessoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>()).Returns([]);

        Result resultado = await ConferirAsync(cenario);

        resultado.IsSuccess.Should().BeTrue();
        cenario.Servicos.Codecs.DidNotReceiveWithAnyArgs().Reidratar(default!);
    }

    [Fact(DisplayName = "O ato publicado de um item que a versão publicada não declara não abre ciclo e a conferência aceita")]
    public async Task AtoDeItemForaDaVersaoPublicada_NaoAbreCiclo_Aceita()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();
        cenario.Servicos.Atos.ListarDoProcessoAsync(cenario.Processo.Id, Arg.Any<CancellationToken>())
            .Returns([AtoDeOutroItem(cenario.Processo.Id)]);

        Result resultado = await ConferirAsync(cenario);

        resultado.IsSuccess.Should().BeTrue();
    }

    private static async Task<Result> ConferirAsync(CenarioDeRetificacaoPublicada cenario)
    {
        VersaoConfiguracao vigente = (await cenario.Servicos.Repositorio.ObterVersaoAtualAsync(cenario.Processo.Id, CancellationToken.None))!;

        return await ConferenciaDoCicloRecursalAberto.ConferirAsync(
            cenario.Servicos.Atos, cenario.Servicos.Codecs, cenario.Processo, vigente, CancellationToken.None);
    }

    private static AtoDoProcesso AtoDeOutroItem(Guid processoId)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar);
        FaseCronograma fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(Guid.CreateVersion7(), "ANALISE", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(1, Inicio: Instante, Fim: Instante.AddDays(10), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [preliminar], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

        return AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, Materia, "Resultado da prova", "1/2027", new DateOnly(2027, 1, 20),
                "Diretor do CEPS", "ceps", "ps-eixos", Guid.CreateVersion7(), [], ItemDoEdital: IdentidadeDoItemDoEdital.DaFase(fase, preliminar)),
            Instante).Value!;
    }
}
