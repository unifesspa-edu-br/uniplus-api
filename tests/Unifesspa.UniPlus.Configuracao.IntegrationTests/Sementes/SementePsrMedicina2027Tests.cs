namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A configuração do edital de Medicina 2027 existe ao subir o sistema, gravada pelas sementes do módulo, e é lida
/// pelos mesmos caminhos que a aplicação usa.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class SementePsrMedicina2027Tests
{
    private static readonly string[] CodigosDosFatos =
    [
        "SOLICITA_FATOR_ATENUACAO", "CAMINHO_FATOR_ATENUACAO", "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO",
        "SOLICITA_ATENDIMENTO_ESPECIALIZADO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO",
        "CERTIFICADO_EM_EMITIDO", "HABILITACAO_POR_PROCURADOR", "VINCULO_OUTRA_IES_PUBLICA_OU_PROUNI", "CATEGORIA_RENDA",
        "CATEGORIAS_RENDA_FAMILIA",
    ];

    private const string CodigoDoTermoDoBancoCentral = "AUTORIZACAO_DE_CONSULTA_AO_BANCO_CENTRAL";

    private readonly ConfiguracaoDbFixture _fixture;

    public SementePsrMedicina2027Tests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Depois da preparaÃ§Ã£o do banco, o tipo PSR, os termos promovidos e os fatos do administrador, com descrição e valores, são lidos")]
    public async Task PreparacaoDoBanco_GravaOTipoOsTermosEOsFatos()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        TipoProcesso tipo = await ctx.TiposProcesso.SingleAsync(t => t.Codigo == SementePsrMedicina2027.TipoProcessoCodigo);
        tipo.Nome.Should().Be("Processo Seletivo Regular Unificado");
        tipo.Ativo.Should().BeTrue();

        await ConferirTermosPromovidosAsync(ctx);
        await ConferirFatosDoAdministradorAsync(ctx);
    }

    [Fact(DisplayName = "Nos modelos semeados, a pergunta de sim ou não termina em interrogação, e a ajuda não manda responder nem repete o rótulo")]
    public async Task Modelos_RotulosEAjudasSemRepeticao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<ModeloFormulario> modelos = await ModelosSemeadosAsync(ctx);
        List<ItemDoModelo> itens = [.. modelos.SelectMany(static m => m.Conteudo.Itens.Concat(m.Conteudo.Grupos.SelectMany(static g => g.Subitens)))];

        modelos.Should().HaveCount(3);
        itens.Where(static i => i.Apresentacao.TipoDeCampo == TipoDeCampo.SimOuNao)
            .Should().OnlyContain(static i => i.Apresentacao.Rotulo.EndsWith('?'), "o sim ou não responde a uma pergunta");
        ConferirAjudasSemRepeticao(itens);
    }

    [Fact(DisplayName = "Reaplicar a semente não duplica nem altera o que já existe")]
    public async Task Reaplicar_NaoDuplicaNemAltera()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        string antes = await LerEstadoAsync(ctx);

        foreach (ComandoDaSemente comando in SementePsrMedicina2027.ComandosDoTipoTermosEFatos())
        {
            await ctx.Database.ExecuteSqlRawAsync(comando.Texto);
        }

        (await LerEstadoAsync(ctx)).Should().Be(antes);
    }

    [Fact(DisplayName = "Nos modelos de inscrição, a ajuda da residência para o Fator cita o período de 20/11/2024 a 20/11/2026 e a continuidade ininterrupta")]
    public async Task ModelosDeInscricao_AjudaDaResidenciaParaOFator_InformaPeriodoEContinuidade()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<ModeloFormulario> modelos = await ModelosSemeadosAsync(ctx);
        modelos.Should().HaveCount(3);
        foreach (ModeloFormulario modelo in modelos.Where(static m => m.Codigo != SementePsrMedicina2027.ModeloDeHabilitacao))
        {
            string? ajuda = modelo.Conteudo.Itens.Single(static i => i.FatoCodigo == "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO").Apresentacao.Ajuda;
            ajuda.Should().Contain("20/11/2024 a 20/11/2026").And.Contain("ininterrupto");
        }
    }

    [Fact(DisplayName = "Nenhum texto dos modelos de inscrição e habilitação semeados cita outubro de 2024 ou outubro de 2026")]
    public async Task ModelosDeInscricaoEHabilitacao_Textos_NaoCitamOutubroDe2024Ou2026()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<ModeloFormulario> modelos = await ModelosSemeadosAsync(ctx);
        modelos.Should().HaveCount(3);
        modelos.SelectMany(TextosDoModelo).Where(static t => t is not null)
            .Should().OnlyContain(static t => !t!.Contains("outubro de 2024", StringComparison.Ordinal) && !t.Contains("outubro de 2026", StringComparison.Ordinal));
    }

    private static IEnumerable<string?> TextosDoModelo(ModeloFormulario modelo) =>
        new[] { modelo.Nome, modelo.Descricao, modelo.Conteudo.Titulo }
            .Concat(modelo.Conteudo.Etapas.SelectMany(static e => new[] { e.Titulo, e.Descricao, e.Aviso }))
            .Concat(modelo.Conteudo.Grupos.Select(static g => g.Rotulo))
            .Concat(modelo.Conteudo.Campos().SelectMany(static i => new[] { i.Apresentacao.Rotulo, i.Apresentacao.Ajuda }));

    [Fact(DisplayName = "O termo do Banco Central está só na habilitação, condicionado a CONCORRER_RENDA, e o Fator de Atenuação é pedido uma vez, com o município conforme a forma de conclusão do ensino médio")]
    public async Task Modelos_TermoDoBancoCentralNaHabilitacaoEFatorDeAtenuacaoNaInscricao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario inscricao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoSemProva);
        ModeloFormulario habilitacao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeHabilitacao);

        ConferirTermoDoBancoCentralSoNaHabilitacao(inscricao, habilitacao);
        ConferirFatorDeAtenuacaoPedidoUmaVez(inscricao);
    }

    private static void ConferirTermoDoBancoCentralSoNaHabilitacao(ModeloFormulario inscricao, ModeloFormulario habilitacao)
    {
        inscricao.Conteudo.Termos.Should().NotContain(static t => t.Codigo == CodigoDoTermoDoBancoCentral);
        TermoDoModelo termoDoBancoCentral = habilitacao.Conteudo.Termos.Should()
            .ContainSingle(static t => t.Codigo == CodigoDoTermoDoBancoCentral).Subject;
        termoDoBancoCentral.FatosCitados.Should().BeEquivalentTo(["CONCORRER_RENDA"]);
        habilitacao.Conteudo.Pressupostos.Should().Contain("CONCORRER_RENDA");
    }

    private static void ConferirFatorDeAtenuacaoPedidoUmaVez(ModeloFormulario inscricao)
    {
        Dictionary<string, ItemDoModelo> itens = inscricao.Conteudo.Itens.ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);
        itens["SOLICITA_FATOR_ATENUACAO"].FatosCitados.Should().BeEmpty("o pedido é feito a todo candidato");
        itens["SOLICITA_FATOR_ATENUACAO"].Apresentacao.Ajuda.Should().NotContainAny(["reside", "conta de"]);
        itens["CAMINHO_FATOR_ATENUACAO"].FatosCitados.Should().BeEquivalentTo(["SOLICITA_FATOR_ATENUACAO", "FORMA_CONCLUSAO_EM"]);
        itens["MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO"].FatosCitados.Should()
            .BeEquivalentTo(["SOLICITA_FATOR_ATENUACAO", "FORMA_CONCLUSAO_EM", "CAMINHO_FATOR_ATENUACAO"]);
        itens["MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO"].FatosCitados.Should().BeEquivalentTo(["SOLICITA_FATOR_ATENUACAO", "CAMINHO_FATOR_ATENUACAO"]);
        string[] doFator = ["CAMINHO_FATOR_ATENUACAO", "MUNICIPIO_ENSINO_MEDIO_FATOR_ATENUACAO", "MUNICIPIO_RESIDENCIA_FATOR_ATENUACAO"];
        doFator.Should().OnlyContain(c => itens[c].Regras.Obrigatoriedade.Tipo == TipoObrigatoriedade.Sempre, "o que aparece é exigido");
    }

    [Fact(DisplayName = "O modelo de inscrição semeado traz a seção dos dados básicos, e as regras dele citam a cor ou raça")]
    public async Task Inscricao_TrazOsDadosBasicosECitaACorOuRaca()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario inscricao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoSemProva);

        inscricao.Conteudo.Etapas.Should().Contain(static e => e.Codigo == ConjuntoBasicoDaInscricao.CodigoDaSecao);
        inscricao.Conteudo.Itens.Where(static i => i.EtapaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao).Select(static i => i.FatoCodigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.Itens.Select(static i => i.FatoCodigo));
        inscricao.Conteudo.Itens.Single(static i => i.FatoCodigo == "CONCORRER_PPI").FatosCitados.Should().Contain("COR_RACA");
    }

    [Fact(DisplayName = "A inscrição com prova pede o atendimento especializado em seção própria: quem pede escolhe a condição e o recurso, e descreve a condição fora da lista")]
    public async Task Inscricao_PedeOAtendimentoEspecializadoEmSecaoPropria()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario inscricao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoComProva);
        Dictionary<string, ItemDoModelo> itens = inscricao.Conteudo.Itens.ToDictionary(static i => i.FatoCodigo, StringComparer.Ordinal);

        ConferirSecaoDoAtendimento(inscricao, itens);
        ConferirExibicaoDoAtendimento(itens);
    }

    private static void ConferirSecaoDoAtendimento(ModeloFormulario inscricao, Dictionary<string, ItemDoModelo> itens)
    {
        inscricao.Conteudo.Etapas.Should().ContainSingle(static e => e.Codigo == "ATENDIMENTO_ESPECIALIZADO")
            .Which.Titulo.Should().Be("Atendimento especializado");
        string[] daSecao = ["SOLICITA_ATENDIMENTO_ESPECIALIZADO", "CONDICAO_ATENDIMENTO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "RECURSO_ACESSIBILIDADE"];
        daSecao.Should().OnlyContain(c => itens[c].EtapaCodigo == "ATENDIMENTO_ESPECIALIZADO");
        daSecao.Should().OnlyContain(c => itens[c].Regras.Obrigatoriedade.Tipo == TipoObrigatoriedade.Sempre, "o que aparece é exigido");
    }

    private static void ConferirExibicaoDoAtendimento(Dictionary<string, ItemDoModelo> itens)
    {
        itens["SOLICITA_ATENDIMENTO_ESPECIALIZADO"].FatosCitados.Should().BeEmpty("o pedido é feito a todo candidato");
        Exibicao(itens["CONDICAO_ATENDIMENTO"]).Should().Be(Se("SOLICITA_ATENDIMENTO_ESPECIALIZADO", true));
        Exibicao(itens["RECURSO_ACESSIBILIDADE"]).Should().Be(Se("SOLICITA_ATENDIMENTO_ESPECIALIZADO", true));
        Exibicao(itens["DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO"]).Should().Be(Se("CONDICAO_ATENDIMENTO", SementePsrMedicina2027.CondicaoForaDaLista));
        itens["TIPO_DEFICIENCIA"].EtapaCodigo.Should().Be("RESERVA_DE_VAGAS", "a etapa do tipo de deficiência não muda nesta configuração");
        itens["DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO"].Regras.Restricoes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new TamanhoTexto(null, 120));
    }

    [Fact(DisplayName = "A inscrição sem prova não pergunta por atendimento especializado nem exige os documentos dele, e as duas versões mantêm o tipo de deficiência da reserva de vagas")]
    public async Task InscricaoSemProva_NaoTrazOAtendimentoEspecializado()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario semProva = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoSemProva);
        ModeloFormulario comProva = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoComProva);

        string[] doAtendimento = ["SOLICITA_ATENDIMENTO_ESPECIALIZADO", "CONDICAO_ATENDIMENTO", "DESCRICAO_OUTRA_CONDICAO_ATENDIMENTO", "RECURSO_ACESSIBILIDADE"];
        semProva.Conteudo.Itens.Select(static i => i.FatoCodigo).Should().NotContain(doAtendimento);
        semProva.Conteudo.Etapas.Should().NotContain(static e => e.Codigo == "ATENDIMENTO_ESPECIALIZADO");
        semProva.Conteudo.Documentos.DocumentosExigidos.Should().BeEmpty();

        semProva.Conteudo.Itens.Should().Contain(static i => i.FatoCodigo == "TIPO_DEFICIENCIA");
        comProva.Conteudo.Itens.Should().Contain(static i => i.FatoCodigo == "TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "A inscrição com prova exige de quem solicita atendimento o laudo de cada condição ofertada e a certidão da lactante, com a base legal resolvida")]
    public async Task Inscricao_TrazOsDocumentosDoAtendimentoEspecializado()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        ModeloFormulario inscricao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoComProva);
        ModeloFormulario habilitacao = await ctx.ModelosFormulario.SingleAsync(m => m.Codigo == SementePsrMedicina2027.ModeloDeHabilitacao);

        ConferirExigenciaDeTodosOsDocumentos(inscricao);
        ConferirLaudoDeCadaCondicaoOfertada(inscricao);
        ConferirCertidaoDaLactante(inscricao);
        habilitacao.Conteudo.Documentos.DocumentosExigidos.Should().BeEmpty();
    }

    private static void ConferirExigenciaDeTodosOsDocumentos(ModeloFormulario inscricao)
    {
        inscricao.Conteudo.Documentos.DocumentosExigidos.Should().HaveCount(2);
        inscricao.Conteudo.Documentos.DocumentosExigidos.Should().OnlyContain(static d => d.FaseCodigo == "INSCRICAO" && d.Indeferimento.Consequencia == "REMOVE_VANTAGEM" && d.Indeferimento.Obrigatorio);
        inscricao.Conteudo.Documentos.DocumentosExigidos.SelectMany(static d => d.Indeferimento.BasesLegais).Should().OnlyContain(static b => b.Situacao == "RESOLVIDO" && b.Abrangencia == "FEDERAL");
    }

    // O laudo não enumera as condições: vale para cada uma que o processo ofertar, menos a lactante e
    // a pessoa idosa.
    private static void ConferirLaudoDeCadaCondicaoOfertada(ModeloFormulario inscricao)
    {
        DocumentoExigidoDoModelo laudo = inscricao.Conteudo.Documentos.DocumentosExigidos.Should().ContainSingle(static d => d.TipoDocumentoCodigo == "LAUDO_MEDICO").Which;
        laudo.PorOpcao.Should().BeEquivalentTo(new ExigenciaPorOpcao("CONDICAO_ATENDIMENTO", ["LACTANTE", "PESSOA_IDOSA"]));
        laudo.Quando!.Clausulas.Single().Condicoes.Select(static c => c.Fato).Should().Equal("SOLICITA_ATENDIMENTO_ESPECIALIZADO");
    }

    private static void ConferirCertidaoDaLactante(ModeloFormulario inscricao)
    {
        DocumentoExigidoDoModelo certidao = inscricao.Conteudo.Documentos.DocumentosExigidos.Should().ContainSingle(static d => d.TipoDocumentoCodigo == "CERTIDAO_NASCIMENTO").Which;
        certidao.PorOpcao.Should().BeNull();
        CondicaoDe(certidao).Should().Be("LACTANTE");
        certidao.Indeferimento.BasesLegais.Select(static b => b.Referencia).Should().Equal("Lei nº 13.872/2019, art. 2º, § 2º");
    }

    private static async Task<List<ModeloFormulario>> ModelosSemeadosAsync(UniPlusDbContext ctx) =>
        await ctx.ModelosFormulario
            .Where(m => m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoSemProva
                || m.Codigo == SementePsrMedicina2027.ModeloDeInscricaoComProva
                || m.Codigo == SementePsrMedicina2027.ModeloDeHabilitacao)
            .ToListAsync();

    private static void ConferirAjudasSemRepeticao(List<ItemDoModelo> itens) =>
        itens.Where(static i => i.Apresentacao.Ajuda != null).Should().OnlyContain(
            static i => !i.Apresentacao.Ajuda!.StartsWith("Responda", StringComparison.Ordinal)
                && !i.Apresentacao.Ajuda.Contains(i.Apresentacao.Rotulo.TrimEnd('?'), StringComparison.OrdinalIgnoreCase),
            "a ajuda diz o que o rótulo não diz");

    private static async Task ConferirTermosPromovidosAsync(UniPlusDbContext ctx)
    {
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await new TermoConsentimentoReader(ctx).ListarVersoesAsync(
            [.. SementePsrMedicina2027.Termos.Select(static t => t.VersaoId)], CancellationToken.None);
        versoes.Select(static v => v.TermoId).Should().BeEquivalentTo(SementePsrMedicina2027.Termos.Select(static t => t.TermoId));
        versoes.Should().OnlyContain(static v => v.FormaAceite == FormaAceite.RegistroComEnderecoIp && v.Hash.Length == 64);
    }

    private static async Task ConferirFatosDoAdministradorAsync(UniPlusDbContext ctx)
    {
        List<FatoCandidato> fatos = await ctx.FatosCandidato
            .Include(static f => f.ValoresDominioDeclarados)
            .Where(f => CodigosDosFatos.Contains(f.Codigo))
            .ToListAsync();
        fatos.Select(static f => f.Codigo).Should().BeEquivalentTo(CodigosDosFatos);
        fatos.Should().OnlyContain(static f => !f.SemeadoPeloSistema && f.Ativo && !string.IsNullOrWhiteSpace(f.Descricao));
        fatos.Single(static f => f.Codigo == "CATEGORIA_RENDA").ValoresDominioDeclarados.Should().HaveCount(7);
        fatos.Single(static f => f.Codigo == "CATEGORIAS_RENDA_FAMILIA").Binding.Should().Be("RESUMO_DE_GRUPO:CATEGORIA_RENDA");
    }

    /// <summary>A condição de atendimento que dispara o documento: cada documento tem uma só cláusula.</summary>
    private static string CondicaoDe(DocumentoExigidoDoModelo documento) =>
        documento.Quando!.Clausulas.Single().Condicoes.Single(static k => k.Fato == "CONDICAO_ATENDIMENTO").Valor.GetString()!;

    /// <summary>A exibição do item, na forma canônica em que o modelo a grava.</summary>
    private static string? Exibicao(ItemDoModelo item) =>
        item.Regras.Exibicao is null ? null : PredicadoDnfJson.ParaJson(item.Regras.Exibicao).ToJsonString();

    /// <summary>A condição única "fato igual a valor", na mesma forma canônica.</summary>
    private static string Se<T>(string fato, T valor) =>
        PredicadoDnfJson.ParaJson(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(1, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!).ToJsonString();

    [Fact(DisplayName = "Fato que o domínio recusa derruba a geração da semente, em vez de virar linha que a API não aceita")]
    public void FatoRecusadoPeloDominio_DerrubaAGeracao()
    {
        SementePsrMedicina2027.FatoDaSemente invalido = new(
            99,
            _ => CriacaoDoFato.CriarDoAdministrador(
                new DescritivoDoFato("codigo fora do padrao", "Fato inválido", "Descrição"),
                new DefinicaoDoFato(TipoDoDado.SimOuNao, QuantidadeDeValores.ValorUnico, null, null, "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal, "Finalidade", HipoteseLegalTratamento.CumprimentoObrigacaoLegal)),
            []);

        Action gerar = () => SementePsrMedicina2027.ComandosDosFatos([invalido]);

        gerar.Should().Throw<InvalidOperationException>().WithMessage("*fato 99*");
    }

    /// <summary>A contagem e o conteúdo das linhas que a semente grava, para comparar antes e depois.</summary>
    private static async Task<string> LerEstadoAsync(UniPlusDbContext ctx)
    {
        List<string> linhas = await ctx.Database.SqlQueryRaw<string>(
            """
            SELECT concat_ws('|', 'tipo', codigo, nome) AS "Value" FROM configuracao.tipos_processo WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'termo', nome, texto_rascunho) FROM configuracao.termos_de_consentimento WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'versao', hash) FROM configuracao.versoes_do_termo_consentimento WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'fato', codigo, nome, descricao) FROM configuracao.dados_do_candidato WHERE id::text LIKE '5e3d%'
            UNION ALL SELECT concat_ws('|', 'valor', codigo, descricao) FROM configuracao.opcoes_dos_dados WHERE id::text LIKE '5e3d%'
            """).ToListAsync();
        return string.Join("\n", linhas.Order(StringComparer.Ordinal));
    }
}
