namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Chaves;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Monta a exigência de documento do domínio: geral, obrigatória, em fase nova, de um tipo de documento com proteção
/// pessoal, com qualquer formato, sem prazo de emissão e sem exigir validade (publicável), salvo o que o teste diz.
/// Cada método devolve um construtor novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record DocumentoExigidoDeTeste
{
    private static readonly ChavesEmUso ChavesFormadas = new();

    private Guid FaseId { get; init; } = Guid.CreateVersion7();

    private Guid? EtapaId { get; init; }

    private FinalidadeFormulario? Finalidade { get; init; }

    private Guid TipoDeOrigemId { get; init; } = Guid.CreateVersion7();

    private string TipoCodigo { get; init; } = "LAUDO";

    private string TipoNome { get; init; } = "Laudo médico";

    private string TipoCategoria { get; init; } = "SAUDE";

    private ProtecaoDoDocumento Protecao { get; init; } = ProtecaoDoDocumentoDeTeste.Pessoal;

    private Aplicabilidade? TipoDeAplicabilidade { get; init; } = Aplicabilidade.Geral;

    private IReadOnlyList<CondicaoGatilho> Condicoes { get; init; } = [];

    private bool Obrigatorio { get; init; } = true;

    private string? Consequencia { get; init; }

    private IReadOnlyList<DocumentoExigidoBaseLegal> BasesLegais { get; init; } = [];

    private FormatosPermitidos Formatos { get; init; } = FormatosPermitidos.Criar(true, null).Value!;

    private int? TamanhoMaximoBytes { get; init; }

    private PrazoDeEmissao? PrazoDeEmissao { get; init; } = new PrazoDeEmissaoBuilder().SemPrazo().Construir();

    private ValidadeDoDocumento? Validade { get; init; } = new ValidadeDoDocumentoBuilder().NaoExige().Construir();

    private ModeloDaExigencia? Modelo { get; init; }

    private IReadOnlyList<string>? FatosComprovados { get; init; }

    public static DocumentoExigidoDeTeste Novo() => new();

    public DocumentoExigidoDeTeste NaFase(Guid faseId) => this with { FaseId = faseId };

    public DocumentoExigidoDeTeste NaEtapa(Guid? etapaId) => this with { EtapaId = etapaId };

    public DocumentoExigidoDeTeste NaFinalidade(FinalidadeFormulario? finalidade) => this with { Finalidade = finalidade };

    public DocumentoExigidoDeTeste ComTipoDeOrigem(Guid tipoDeOrigemId) => this with { TipoDeOrigemId = tipoDeOrigemId };

    public DocumentoExigidoDeTeste ComTipo(string codigo, string nome, string categoria) =>
        this with { TipoCodigo = codigo, TipoNome = nome, TipoCategoria = categoria };

    public DocumentoExigidoDeTeste ComProtecao(ProtecaoDoDocumento protecao) => this with { Protecao = protecao };

    public DocumentoExigidoDeTeste ComAplicabilidade(Aplicabilidade? aplicabilidade) => this with { TipoDeAplicabilidade = aplicabilidade };

    public DocumentoExigidoDeTeste ComCondicoes(IReadOnlyList<CondicaoGatilho> condicoes) => this with { Condicoes = condicoes };

    public DocumentoExigidoDeTeste Condicional(IReadOnlyList<CondicaoGatilho> condicoes) =>
        this with { TipoDeAplicabilidade = Aplicabilidade.Condicional, Condicoes = condicoes };

    public DocumentoExigidoDeTeste ComObrigatorio(bool obrigatorio) => this with { Obrigatorio = obrigatorio };

    public DocumentoExigidoDeTeste Facultativa() => this with { Obrigatorio = false };

    public DocumentoExigidoDeTeste ComConsequencia(string? consequencia) => this with { Consequencia = consequencia };

    public DocumentoExigidoDeTeste ComBasesLegais(IReadOnlyList<DocumentoExigidoBaseLegal> basesLegais) => this with { BasesLegais = basesLegais };

    public DocumentoExigidoDeTeste ComFormatos(FormatosPermitidos formatos) => this with { Formatos = formatos };

    public DocumentoExigidoDeTeste ComTamanhoMaximo(int? tamanhoMaximoBytes) => this with { TamanhoMaximoBytes = tamanhoMaximoBytes };

    public DocumentoExigidoDeTeste ComPrazoDeEmissao(PrazoDeEmissao? prazo) => this with { PrazoDeEmissao = prazo };

    public DocumentoExigidoDeTeste ComValidade(ValidadeDoDocumento? validade) => this with { Validade = validade };

    public DocumentoExigidoDeTeste ComModelo(ModeloDaExigencia? modelo) => this with { Modelo = modelo };

    public DocumentoExigidoDeTeste ComFatosComprovados(IReadOnlyList<string>? fatos) => this with { FatosComprovados = fatos };

    public Result<DocumentoExigido> Criar() => DocumentoExigido.Criar(NovaChave(), Declarado());

    public DocumentoExigido Reidratar(Guid id) => DocumentoExigido.Reidratar(NovaChave(), id, Declarado());

    private static ChaveLocal NovaChave(string codigoDoTipo)
    {
        lock (ChavesFormadas)
        {
            ChaveLocal chave = FormacaoDeChaveLocal.Formar("exigencia", codigoDoTipo, ChavesFormadas);
            ChavesFormadas.Adicionar(chave);
            return chave;
        }
    }

    private ChaveLocal NovaChave() => NovaChave(TipoCodigo);

    private DocumentoExigidoDeclarado Declarado() =>
        new(TipoDoDocumento(), MomentoDaColeta(), AplicabilidadeDeclarada(), IndeferimentoDeclarado(), RequisitosDoArquivo(), FatosComprovados);

    private CopiaDoTipoDeDocumento TipoDoDocumento() => new(TipoDeOrigemId, TipoCodigo, TipoNome, TipoCategoria, Protecao);

    private MomentoDaExigencia MomentoDaColeta() => new(FaseId, EtapaId, Finalidade);

    private AplicabilidadeDaExigencia AplicabilidadeDeclarada() => new(TipoDeAplicabilidade, Condicoes);

    private IndeferimentoDoDocumento IndeferimentoDeclarado() => new(Obrigatorio, Consequencia, BasesLegais);

    private RequisitosDoDocumento RequisitosDoArquivo() => new(Formatos, TamanhoMaximoBytes, Modelo, PrazoDeEmissao, Validade);
}
