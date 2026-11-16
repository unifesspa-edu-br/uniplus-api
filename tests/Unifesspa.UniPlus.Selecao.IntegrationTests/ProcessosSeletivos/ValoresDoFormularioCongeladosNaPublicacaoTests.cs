namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Issue #1059 (UNI-REQ-0072) — imunidade do formulário público ao que acontece <b>depois</b>
/// da publicação, com Postgres real (Testcontainers): os valores selecionáveis congelados na
/// publicação voltam idênticos em leituras repetidas e só mudam por retificação.
/// </summary>
/// <remarks>
/// <para>
/// <b>O que esta suíte prova</b>: <c>ObterFormularioRenderizavelQueryHandler.Handle</c> lê
/// SOMENTE <c>VersaoConfiguracao.ConfiguracaoCongelada</c> — nenhum leitor de catálogo entra na
/// sua assinatura (prova estrutural em
/// <see cref="ObterFormularioRenderizavelQueryHandlerTests.Handle_NaoDependeDeLeitorDeCatalogo"/>).
/// O que esta suíte acrescenta é a prova EMPÍRICA, com banco real: os mesmos bytes persistidos
/// no ato de publicação são o que volta em leituras independentes e repetidas — sem cache, sem
/// estado compartilhado entre chamadas, sem qualquer efeito de "o que aconteceu entre uma
/// leitura e outra". As duas provas juntas — assinatura fechada e leitura estável através de
/// Postgres real — são o que sustenta a garantia de imunidade ao catálogo vivo.
/// </para>
/// <para>
/// <b>Decisão de escopo — o que esta suíte NÃO faz</b>: o desenho original desta prova (o
/// plano da issue) previa inativar de fato um valor no catálogo do módulo Configuração e reler
/// o formulário. Reproduzir isso literalmente exigiria uma segunda infraestrutura de banco
/// (<c>UniPlusDbContext</c>, que este projeto de testes não provisiona) e um leitor de
/// catálogo real — escopo maior que esta suíte, e redundante com a garantia estrutural: se o
/// handler não tem PARÂMETRO de leitor de catálogo, inativar um valor no catálogo não tem como
/// alcançar esta leitura, qualquer que seja o valor. Fica registrado como lacuna de cobertura
/// explícita, não como comportamento não verificado.
/// </para>
/// </remarks>
[Collection(FormularioRenderizavelPersistenciaColecao.Nome)]
public sealed class ValoresDoFormularioCongeladosNaPublicacaoTests
{
    private readonly CenarioDeFormularioPublicado _cenario;

    public ValoresDoFormularioCongeladosNaPublicacaoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeFormularioPublicado(fixture);
    }

    [Fact(DisplayName = "O formulário lido do banco real reproduz os valores selecionáveis congelados na publicação — leituras independentes e repetidas são idênticas")]
    public async Task Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes()
    {
        ProcessoSeletivo processo = CenarioDeFormularioPublicado.NovoProcessoComFatoDeSelecao(
            nameof(Handle_ComPostgresReal_ReproduzOsValoresCongeladosEmLeiturasIndependentes));

        const string hashDocumento = "2222222222222222222222222222222222222222222222222222222222222222";
        DadosEdital dados = CenarioDeFormularioPublicado.DadosDoEdital(Guid.CreateVersion7());

        // As SEIS categorias do IBGE — o vocabulário completo que o catálogo declarava íntegro
        // no instante da publicação. É este dicionário, e só ele, que o formulário lido depois
        // tem de reproduzir — nunca uma consulta nova ao catálogo vivo.
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = [
                new ValorDominioDeclaradoCongelado("BRANCA", "Autodeclaração de cor/raça branca.", 0),
                new ValorDominioDeclaradoCongelado("PRETA", "Autodeclaração de cor/raça preta.", 1),
                new ValorDominioDeclaradoCongelado("PARDA", "Autodeclaração de cor/raça parda.", 2),
                new ValorDominioDeclaradoCongelado("AMARELA", "Autodeclaração de cor/raça amarela.", 3),
                new ValorDominioDeclaradoCongelado("INDIGENA", "Autodeclaração de cor/raça indígena.", 4),
                new ValorDominioDeclaradoCongelado("NAO_DECLARADO", "Prefere não declarar.", 5),
            ],
        };

        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(valoresSelecionaveis),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        await _cenario.PublicarEPersistirAsync(processo, dados, congelado, hashDocumento, "integration-test-user");

        // Duas leituras INDEPENDENTES — dois DbContext distintos, a mesma separação que existe
        // entre duas requisições HTTP de fato diferentes. Nenhum leitor de catálogo é injetado:
        // se a resposta dependesse de qualquer estado externo ao byte persistido — cache, campo
        // estático, ordem de execução — as duas leituras poderiam divergir entre si.
        FormularioRenderizavelDto primeira = await _cenario.LerPublicadoAsync(processo.Id);
        FormularioRenderizavelDto segunda = await _cenario.LerPublicadoAsync(processo.Id);

        CampoRenderizavel corRaca = primeira.FatosColetados.Single(f => f.FatoCodigo == "COR_RACA");
        corRaca.ValoresSelecionaveis.Should().NotBeNull();
        corRaca.ValoresSelecionaveis!.Select(v => v.Codigo).Should().Equal(
            ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_DECLARADO"],
            "o formulário lido do banco traz os SEIS valores congelados na publicação, na ordem canônica — " +
            "os mesmos que o catálogo declarava íntegro no instante em que o certame foi publicado");

        // O valor de uma condição é JSON: as duas leituras se comparam pelo texto dele.
        segunda.Should().BeEquivalentTo(primeira, opcoes => opcoes
                .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>(),
            "duas leituras independentes da MESMA versão publicada — sem catálogo algum na assinatura do " +
            "handler — não têm de onde divergir: o formulário é função pura dos bytes persistidos");
    }

    [Fact(DisplayName = "Corrigir o texto de um valor no catálogo muda o rascunho, e a versão publicada mantém o texto congelado")]
    public async Task Handle_ValorCorrigidoNoCatalogo_RascunhoLeOVivoEPublicadoOCongelado()
    {
        (ProcessoSeletivo processo, _) = await PublicarComCorRacaAsync(nameof(Handle_ValorCorrigidoNoCatalogo_RascunhoLeOVivoEPublicadoOCongelado));

        FormularioRenderizavelDto rascunho = await _cenario.LerRascunhoAsync(processo.Id, CatalogoComCorRaca([.. CorRacaCorrigida()]));

        rascunho.FatosColetados.Single(static f => f.FatoCodigo == "COR_RACA").ValoresSelecionaveis!
            .Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao)).Should().Equal(
                ("PRETA", "Preta ou negra.", 0, "Escolha conforme a sua autodeclaração."),
                ("BRANCA", "Branca (corrigida).", 1, null));

        FormularioRenderizavelDto publicado = await _cenario.LerPublicadoAsync(processo.Id);
        publicado.FatosColetados.Single(static f => f.FatoCodigo == "COR_RACA").ValoresSelecionaveis!
            .Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao)).Should().Equal(
                ("BRANCA", "Branca.", 0, null),
                ("PRETA", "Preta.", 1, null));
    }

    [Fact(DisplayName = "A retificação congela a descrição, a orientação e a ordem que o catálogo tem na hora; a versão anterior não muda")]
    public async Task Retificar_ValorCorrigidoNoCatalogo_VersaoNovaCongelaOTextoEAOrdemNovos()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao abertura) =
            await PublicarComCorRacaAsync(nameof(Retificar_ValorCorrigidoNoCatalogo_VersaoNovaCongelaOTextoEAOrdemNovos));

        VersaoConfiguracao retificacao;
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, string.Concat(Enumerable.Repeat("ab01234567", 7))[..64], TimeProvider.System).IsSuccess.Should().BeTrue();
        await using (UniPlusDbContext writeContext = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dados = CenarioDeFormularioPublicado.DadosDoEdital(documento.Id);

            // O catálogo já traz a correção quando a retificação congela.
            DefinicaoAvaliavel naRetificacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(carregado, CatalogoComCorRaca([.. CorRacaCorrigida()])).Value!;
            SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(new EntradaCanonicalizacao(
                carregado, dados, documento.HashSha256!, FusoInstitucional.ZoneId,
                new RetificacaoDoEdital(versaoAtual.AtoCriadorId, "Correção do texto das opções"),
                ValoresSelecionaveisCongelados: naRetificacao.ValoresSelecionaveis,
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(carregado), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
            Result<VersaoConfiguracao> retificar = carregado.Retificar(
                dados, versaoAtual, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
                documento.HashSha256!, "integration-test-user", "Correção do texto das opções", TimeProvider.System, CorpusEnvelopeDadosDoEdital.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            retificacao = retificar.Value!;

            await writeContext.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(retificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == abertura.Id, CancellationToken.None);
        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.Id == retificacao.Id, CancellationToken.None);

        Projetados(CenarioDeFormularioPublicado.RegistroCodecs.Reidratar(retificacaoLida).Value!.ValoresSelecionaveisCongelados!["COR_RACA"]!).Should().Equal(
            ("PRETA", "Preta ou negra.", 0, "Escolha conforme a sua autodeclaração."),
            ("BRANCA", "Branca (corrigida).", 1, null));
        Projetados(CenarioDeFormularioPublicado.RegistroCodecs.Reidratar(aberturaLida).Value!.ValoresSelecionaveisCongelados!["COR_RACA"]!).Should().Equal(
            ("BRANCA", "Branca.", 0, null),
            ("PRETA", "Preta.", 1, null));
    }

    private static IReadOnlyList<FatoCandidatoView> CatalogoComCorRaca(params FatoValorDominioViewItem[] valores) => CatalogoDoConjuntoBasico.Com(
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "COR_RACA", "Cor ou raça", "Descrição do fato.", "CATEGORICO", "DECLARADO", "ESCALAR",
            [.. valores.Select(static v => v.Codigo)], "INSCRICAO", "CAMPO_FORMULARIO:COR_RACA", [.. valores], "GLOBAL",
            Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ]);

    private static IReadOnlyList<FatoValorDominioViewItem> CorRacaComoCadastrada() =>
    [
        new("BRANCA", "Branca.", 0, true),
        new("PRETA", "Preta.", 1, true),
    ];

    private static IReadOnlyList<FatoValorDominioViewItem> CorRacaCorrigida() =>
    [
        new("PRETA", "Preta ou negra.", 0, true, "Escolha conforme a sua autodeclaração."),
        new("BRANCA", "Branca (corrigida).", 1, true),
    ];

    private static (string Codigo, string? Descricao, int Ordem, string? Orientacao)[] Projetados(IEnumerable<ValorDominioDeclaradoCongelado> valores) =>
        [.. valores.OrderBy(static v => v.Ordem).Select(static v => (v.Codigo, v.Descricao, v.Ordem, v.Orientacao))];

    private async Task<(ProcessoSeletivo Processo, VersaoConfiguracao Versao)> PublicarComCorRacaAsync(string nome)
    {
        ProcessoSeletivo processo = CenarioDeFormularioPublicado.NovoProcessoComFatoDeSelecao(nome);
        const string hashDocumento = "4444444444444444444444444444444444444444444444444444444444444444";
        DadosEdital dados = CenarioDeFormularioPublicado.DadosDoEdital(Guid.CreateVersion7());
        DefinicaoAvaliavel naPublicacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, CatalogoComCorRaca([.. CorRacaComoCadastrada()])).Value!;
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: naPublicacao.ValoresSelecionaveis,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        VersaoConfiguracao versao = await _cenario.PublicarEPersistirAsync(processo, dados, congelado, hashDocumento, "integration-test-user");
        return (processo, versao);
    }
}
