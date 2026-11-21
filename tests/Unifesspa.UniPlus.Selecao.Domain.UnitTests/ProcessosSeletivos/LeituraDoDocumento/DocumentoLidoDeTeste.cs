namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O documento e os cadastros citados que os testes da leitura montam: o que não declaram tem o valor de um documento completo e conforme.</summary>
internal static class DocumentoLidoDeTeste
{
    internal const string CodigoDaConvencao = "CONTAGEM-DE-TESTE";
    internal const string CodigoDoProcedimento = "PROCEDIMENTO-DE-TESTE";
    internal const string AtoDoResultado = "RESULTADO_PRELIMINAR";
    internal const string AtoDoDefinitivo = "RESULTADO_DEFINITIVO";

    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static readonly DateTimeOffset LidoEm = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    internal static CadastrosCitadosNoDocumento Cadastros() => new(LidoEm)
    {
        Unidades = Unidades(),
        TiposDeProcesso = new Dictionary<string, TipoProcessoSnapshot> { ["SiSU"] = TiposDeProcessoDeTeste.SiSU },
        FasesCanonicas = FasesCanonicas(),
        TiposDeBanca = new Dictionary<string, TipoDeBancaCitado> { ["ANALISE_RECURSOS"] = new(Guid.CreateVersion7(), "ANALISE_RECURSOS", "Banca de análise de recursos") },
        CategoriasDeDocumento = new Dictionary<string, CategoriaDeDocumentoCitada> { ["IDENTIDADE"] = new(Guid.CreateVersion7(), "IDENTIDADE") },
        Localidades = new Dictionary<string, LocalidadeRegente> { ["1504208"] = LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value! },
        Regras = Regras(),
        TiposDeAto = new HashSet<string>(StringComparer.Ordinal) { AtoDoResultado, AtoDoDefinitivo },
    };

    private static Dictionary<string, UnidadeCitada> Unidades() => new()
    {
        ["CEPS"] = Unidade(UnidadesDeTeste.Ceps, "ceps"),
        ["CGA"] = Unidade(UnidadesDeTeste.Cga, "cga"),
    };

    private static UnidadeCitada Unidade(UnidadeResponsavelSnapshot modelo, string slug) =>
        new(modelo.OrigemId, UnidadeAdministradoraSnapshot.Criar(modelo.Sigla, modelo.Sigla, slug, modelo.Nome, "ADMINISTRATIVA").Value!);

    private static Dictionary<string, OrigemCanonicaDaFase> FasesCanonicas() => new()
    {
        ["INSCRICAO"] = FaseCanonica("INSCRICAO"),
        ["RECURSO"] = FaseCanonica("RECURSO"),
        ["RESULTADO"] = FaseCanonica("RESULTADO"),
    };

    private static Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras() => new()
    {
        [(CodigoDaConvencao, "v1")] = Regra(TipoRegra.AlgoritmoContagemPrazo, CodigoDaConvencao),
        [(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1")] = Regra(TipoRegra.RegraPrazoRecurso, RegraPrazoRecursoCodigo.AncoradoEmAto),
        [(CodigoDoProcedimento, "v1")] = Regra(TipoRegra.RegraProcedimentoRecurso, CodigoDoProcedimento),
    };

    internal static DeclaracoesDoDocumento Documento(params FaseNoDocumento[] fases) => new(Identificacao(), fases);

    internal static IdentificacaoNoDocumento Identificacao() =>
        new("Processo Seletivo de Exemplo 2027", "SiSU", "CEPS", OrigemCandidatos.ImportacaoExterna, "1504208", IdentificadoresDeTeste.NovoValor(), new(CodigoDaConvencao, "v1"));

    internal static FaseNoDocumento Fase(int posicao, string chave, string faseCanonica = "INSCRICAO", string? unidade = "CEPS") =>
        new(posicao, chave, new(faseCanonica, unidade), new(null, null), new([], false, null, null), [], null);

    internal static FaseNoDocumento Publicando(this FaseNoDocumento fase, params ProdutoNoDocumento[] produtos) =>
        fase with { Publicacoes = fase.Publicacoes with { Produtos = produtos } };

    internal static BancaNoDocumento Banca(int posicao, string chave, string tipo, string[] categorias, DeclaracaoDaDeliberacao? regra = null) =>
        new(posicao, chave, tipo, categorias, regra);

    internal static CondicaoNoDocumento Condicao(TipoDeCondicaoDeHomologacao? tipo, int edicao, string motivo) => new(tipo, edicao, motivo, null);

    internal static ProdutoNoDocumento Preliminar(int posicao, string ato) => new(posicao, ato, PapelProdutoFase.Preliminar);

    internal static ProdutoNoDocumento Definitivo(int posicao, string ato) => new(posicao, ato, PapelProdutoFase.Definitivo);

    internal static IdentificacaoECronogramaLidos Ler(DeclaracoesDoDocumento documento) =>
        IdentificacaoECronogramaLidos.Ler(documento, Cadastros());

    internal static IdentificacaoECronogramaLidos Ler(IdentificacaoNoDocumento identificacao, params FaseNoDocumento[] fases) =>
        IdentificacaoECronogramaLidos.Ler(new(identificacao, fases), Cadastros());

    internal static CronogramaDoProcesso Cronograma(IdentificacaoECronogramaLidos lidos) =>
        CronogramaDoProcesso.Do(lidos.Cronograma.CronogramaFases, lidos.Origem);

    internal static ConfiguracaoDeclarada Configuracao(IdentificacaoECronogramaLidos lidos)
    {
        ConfiguracaoDeclarada raiz = ProcessoConformeFactory.Criar().Declaracao();

        return new ConfiguracaoDeclarada(
            lidos.Identidade, lidos.Cronograma, raiz.Avaliacao, raiz.Vagas, raiz.Exigencias, raiz.Coleta, raiz.DivulgacaoETaxa)
        {
            Origem = lidos.Origem,
        };
    }

    internal static (string? Ponteiro, string Codigo)[] PartesRecusadas(IdentificacaoECronogramaLidos lidos) =>
        [.. lidos.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    internal static string[] Mensagens(IdentificacaoECronogramaLidos lidos) =>
        [.. lidos.Origem.PartesRecusadas.Select(static parte => parte.Causa.Error.Message)];

    private static OrigemCanonicaDaFase FaseCanonica(string codigo) =>
        new(Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false);

    private static RegraDoCatalogoCitada Regra(TipoRegra tipo, string codigo) => new(tipo, ReferenciaRegra.De(codigo, "v1", Hash));
}
