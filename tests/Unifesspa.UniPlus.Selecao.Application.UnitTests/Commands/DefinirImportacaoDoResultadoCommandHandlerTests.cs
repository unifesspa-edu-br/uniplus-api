namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class DefinirImportacaoDoResultadoCommandHandlerTests
{
    private static readonly RelatorioInformado[] UmRelatorio = [new("INEP_RETORNO_ENEM", "v1")];

    private static ProcessoSeletivo ImportadoComFase(out Guid faseId)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS 2026 — Resultado importado", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
        FaseCronograma fase = HomologacaoDasInscricoesDeTeste.FaseQueHomologaComRecurso(1);
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();
        faseId = fase.Id;
        return processo;
    }

    [Fact(DisplayName = "Com relatórios e a primeira fase do cronograma, o handler grava a declaração e salva")]
    public async Task Handle_DeclaracaoValida_GravaESalva()
    {
        ProcessoSeletivo processo = ImportadoComFase(out Guid fase);
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir(UmRelatorio, fase);

        resultado.IsSuccess.Should().BeTrue();
        processo.ImportacaoDoResultado!.PrimeiraFaseDepoisDaImportacaoId.Should().Be(fase);
        processo.ImportacaoDoResultado.Relatorios.Should().ContainSingle();
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista vazia sem a primeira fase desfaz a declaração, que volta a por declarar, e salva")]
    public async Task Handle_ListaVaziaSemFase_VoltaAPorDeclarar()
    {
        ProcessoSeletivo processo = ImportadoComFase(out Guid fase);
        processo.DefinirImportacaoDoResultado(UmRelatorio, fase).IsSuccess.Should().BeTrue();
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir([], null);

        resultado.IsSuccess.Should().BeTrue();
        processo.ImportacaoDoResultado.Should().BeNull();
        await contexto.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista vazia com a primeira fase informada é aceita na gravação, e a publicação é que a recusa por falta de relatório")]
    public async Task Handle_ListaVaziaComFase_AceitaNaGravacao()
    {
        ProcessoSeletivo processo = ImportadoComFase(out Guid fase);
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir([], fase);

        resultado.IsSuccess.Should().BeTrue();
        processo.ImportacaoDoResultado!.Relatorios.Should().BeEmpty();
        processo.ImportacaoDoResultado.PrimeiraFaseDepoisDaImportacaoId.Should().Be(fase);
    }

    [Fact(DisplayName = "A primeira fase fora do cronograma devolve a recusa no campo e não salva")]
    public async Task Handle_FaseForaDoCronograma_RecusaSemSalvar()
    {
        ProcessoSeletivo processo = ImportadoComFase(out _);
        Contexto contexto = new(processo);

        Result resultado = await contexto.Definir(UmRelatorio, Guid.NewGuid());

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("primeiraFaseDepoisDaImportacaoId", ImportacaoDoResultadoErrorCodes.PrimeiraFaseForaDoCronograma));
        processo.ImportacaoDoResultado.Should().BeNull();
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema recusa a importação, sem salvar")]
    public async Task Handle_InscricaoPeloSistema_RecusaSemSalvar()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir(UmRelatorio, null);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo com inscrição pelo sistema e código malformado recebe só a recusa de origem")]
    public async Task Handle_InscricaoPeloSistemaComCodigoMalformado_SoARecusaDeOrigem()
    {
        Contexto contexto = new(PublicacaoDeTeste.NovoProcessoConforme());

        Result resultado = await contexto.Definir([new("MEU LEIAUTE", null)], null);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ImportacaoDoResultadoErrorCodes.EmInscricaoPeloSistema);
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "No processo importado, o código malformado e a versão ausente são recusados juntos, cada um no seu campo")]
    public async Task Handle_CodigoMalformadoEVersaoAusente_AcumulamAsRecusas()
    {
        Contexto contexto = new(ImportadoComFase(out _));

        Result resultado = await contexto.Definir([new("MEU LEIAUTE", "v1"), new("OUTRO", null)], null);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("relatorios[0].leiauteCodigo", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute),
            ("relatorios[1].leiauteVersao", ImportacaoDoResultadoErrorCodes.RelatorioSemLeiaute));
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A lista de relatórios ausente é recusada como obrigatória, no campo do pedido, sem salvar")]
    public async Task Handle_RelatoriosAusentes_RecusaComoObrigatorio()
    {
        Contexto contexto = new(ImportadoComFase(out _));

        Result resultado = await contexto.Definir(null, null);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("relatorios", CodigosDeValidacao.Obrigatorio));
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O leiaute repetido devolve a recusa do domínio, sem salvar")]
    public async Task Handle_LeiauteRepetido_RecusaSemSalvar()
    {
        Contexto contexto = new(ImportadoComFase(out _));

        Result resultado = await contexto.Definir([new("A", "v1"), new("A", "v2")], null);

        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(ImportacaoDoResultadoErrorCodes.RelatorioRepetido);
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo inexistente devolve não encontrado, sem salvar")]
    public async Task Handle_ProcessoInexistente_RetornaNaoEncontrado()
    {
        Contexto contexto = new(null);

        Result resultado = await contexto.Definir(UmRelatorio, null);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O processo publicado recusa a edição, sem salvar")]
    public async Task Handle_ProcessoPublicado_RecusaSemSalvar()
    {
        Contexto contexto = new(ApoioDaCascataDeRemanejamento.NovoProcessoPublicado());

        Result resultado = await contexto.Definir(UmRelatorio, null);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        await contexto.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O conflito de concorrência descarta o rastreamento e devolve o conflito, sem relançar")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Contexto contexto = new(ImportadoComFase(out _));
        contexto.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("xmin"));

        Result resultado = await contexto.Definir(UmRelatorio, null);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.ConflitoDeConcorrencia");
        contexto.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    private sealed class Contexto
    {
        private readonly Guid _id;
        private readonly IProcessoSeletivoRepository _repositorio = Substitute.For<IProcessoSeletivoRepository>();

        public Contexto(ProcessoSeletivo? processo)
        {
            _id = processo?.Id ?? Guid.NewGuid();
            _repositorio.ObterParaMutacaoAsync(_id, Arg.Any<CancellationToken>()).Returns(processo);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public Task<Result> Definir(IReadOnlyList<RelatorioInformado>? relatorios, Guid? primeiraFaseId) =>
            DefinirImportacaoDoResultadoCommandHandler.Handle(new(_id, relatorios, primeiraFaseId), _repositorio, UnitOfWork, CancellationToken.None);
    }
}
