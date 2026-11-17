namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O formulário de inscrição sob a retificação: o descarte restaura o título e os termos da versão
/// anterior, e a regravação dos fatos coletados repõe os dados de sexo e gênero sem colidir a ordem.
/// </summary>
[Collection(RetificacaoPersistenciaColecao.Nome)]
public sealed class FormularioDeInscricaoNaRetificacaoTests
{
    private readonly CenarioDeRetificacaoPersistida _cenario;

    public FormularioDeInscricaoNaRetificacaoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeRetificacaoPersistida(fixture);
    }

    [Theory(DisplayName = "Descartar a retificação após editar o formulário de inscrição restaura exatamente o título e os termos da versão anterior")]
    [InlineData("Formulário publicado", "TERMO_PUBLICADO", "Formulário editado", "TERMO_EDITADO")]
    [InlineData(null, null, "Formulário editado", "TERMO_EDITADO")]
    public async Task Retificacao_DescartadaAposEditarFormulario_RestauraFormularioDaVersaoAnterior(
        string? tituloPublicado, string? termoPublicado, string tituloEditado, string termoEditado)
    {
        string nome = $"{nameof(Retificacao_DescartadaAposEditarFormulario_RestauraFormularioDaVersaoAnterior)}-{Guid.CreateVersion7()}";
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);
        processo.DefinirTitulo(tituloPublicado, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermos(
            termoPublicado is null ? [] : [CorpusEnvelopeEntrada.Termo(termoPublicado, 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        Guid processoId = processo.Id;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            CenarioDeRetificacaoPersistida.AbrirRetificacao(tracked, "Testar edição e descarte do formulário", versaoAbertura);

            tracked.DefinirTitulo(tituloEditado, PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            // A ordem 0 do termo editado colide com a do publicado: a restauração tem de limpar
            // antes de repor, senão bateria no índice único.
            tracked.DefinirTermos([CorpusEnvelopeEntrada.Termo(termoEditado, 0)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            tracked.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo.Should().Be(tituloEditado, "pré-condição: a sessão editorial trocou o título");
            tracked.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, "Habilitação", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue("a retificação acrescenta formulário");

            await CenarioDeRetificacaoPersistida.DescartarComProvaAsync(sessao, tracked, versaoAbertura);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull(
            "o formulário acrescentado na sessão abandonada some com ela");
        relido.FormularioDe(FinalidadeFormulario.Inscricao)!.Titulo.Should().Be(
            tituloPublicado, "o descarte restaurou o título que a versão N congelou — não o título editado na sessão abandonada");
        relido.TermosExigidos.Select(static t => t.Codigo).Should().Equal(
            termoPublicado is null ? [] : [termoPublicado],
            "o descarte restaurou os termos que a versão N congelou — não os editados na sessão abandonada");
    }

    [Fact(DisplayName = "A retificação de um processo cujo formulário de inscrição não tinha os dados de sexo e gênero repõe os três na regravação, sem colidir a ordem")]
    public async Task Retificacao_FormularioSemOsDadosDeGenero_RegravacaoReporOsItens()
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(
            $"{nameof(Retificacao_FormularioSemOsDadosDeGenero_RegravacaoReporOsItens)}-{Guid.CreateVersion7()}");
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        await using UniPlusDbContext sessao = _cenario.CriarDbContext();
        ProcessoSeletivoRepository processos = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
        ProcessoSeletivo tracked = (await processos.ObterParaMutacaoAsync(processo.Id, CancellationToken.None))!;
        AbrirRetificacaoDeAntesDosDadosDeGenero(tracked, versaoAbertura);
        CodigosDaConferencia(tracked).Should().Contain(
            FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico, "pré-condição: a retificação não fecharia assim");

        await RegravarEAfirmarAsync(processos, sessao, tracked);
    }

    // A versão publicada é a de antes dos três dados: os fatos vigentes não os incluem.
    private static FatosDasVersoesPublicadas PublicadasSemOsDadosDeGenero(ProcessoSeletivo tracked) =>
        VersoesPublicadasDeTeste.SemEixosCongelados(
        [
            VersoesPublicadasDeTeste.Versao(
                tracked.Formularios,
                tracked.FatosColetados.Where(static f => f.FatoCodigo is not ("SEXO_CIVIL" or "IDENTIDADE_GENERO" or "ORIENTACAO_SEXUAL")),
                tracked.GruposColetados),
        ]);

    // O estado de um processo publicado antes dos três dados: a seção sem eles, na numeração de então.
    private static void AbrirRetificacaoDeAntesDosDadosDeGenero(ProcessoSeletivo tracked, VersaoConfiguracao versaoAbertura)
    {
        tracked.AbrirRetificacao(
            "Repor os dados de sexo e gênero", versaoAbertura, identificadorDaVersaoBase: null, PublicadasSemOsDadosDeGenero(tracked),
            "integration-test-user", TimeProvider.System.GetUtcNow()).IsSuccess.Should().BeTrue();
        Result legado = tracked.DefinirFatosColetados(
            FinalidadeFormulario.Inscricao, FormularioDeTeste.DadosBasicosDeAntesDosDadosDeGenero(), PrecondicaoIfMatch.Curinga);
        legado.IsSuccess.Should().BeTrue(legado.Error?.Message + string.Join(";", legado.Errors.Select(static e => e.Error.Message)));
    }

    private static async Task RegravarEAfirmarAsync(ProcessoSeletivoRepository processos, UniPlusDbContext sessao, ProcessoSeletivo tracked)
    {
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com([]));
        Result<MutacaoAceita> regravacao = await DefinirFatosColetadosCommandHandler.Handle(
            new DefinirFatosColetadosCommand(tracked.Id, FinalidadeFormulario.Inscricao, [], PrecondicaoIfMatch.Curinga),
            processos, catalogo, sessao, CancellationToken.None);

        regravacao.IsSuccess.Should().BeTrue(regravacao.Error?.Message);
        CodigosDaConferencia(tracked).Should().NotContain(FormularioProcessoErrorCodes.InscricaoSemConjuntoBasico);
        tracked.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.Ordem)
            .Should().OnlyHaveUniqueItems();
    }

    private static IEnumerable<string> CodigosDaConferencia(ProcessoSeletivo processo) =>
        ConfiguracaoAptaACongelar.Conferir(EstadoDaConfiguracao.De(processo), CadastrosDeTeste.Padrao).Errors.Select(static e => e.Error.Code);
}
