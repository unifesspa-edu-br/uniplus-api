namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// Um fato do candidato no catálogo <c>dados_do_candidato</c> (UNI-REQ-0143, ADR-0136):
/// o que o sistema sabe perguntar ou derivar sobre um candidato. Para cada fato, o seu
/// <see cref="Dominio"/>, a sua <see cref="Origem"/>, a sua <see cref="Cardinalidade"/>, a
/// <see cref="FonteValores"/> do categórico, o <see cref="PontoResolucao"/>, o
/// <see cref="Binding"/>, o <see cref="Escopo"/> e a proteção de dados que o tratamento exige.
/// Não armazena o valor de nenhum candidato.
/// </summary>
/// <remarks>
/// <para>
/// Dois regimes no mesmo cadastro: o fato de <b>sistema</b> é semeado por código e só tem editáveis o
/// nome, a descrição, o componente de exibição e a proteção de dados — a definição, que o sistema calcula ou lê, é travada —; o
/// fato do <b>administrador</b> é cadastrado em tempo de execução. Nos dois regimes, criar o fato ou
/// mudar a proteção o deixa pendente de aprovação da proteção de dados (ADR-0153). Em nenhum dos dois o fato é apagado: desativar bloqueia só
/// vínculos novos, e o processo que já o usa continua com a cópia que congelou (ADR-0061).
/// </para>
/// </remarks>
public sealed class FatoCandidato : EntityBase, IAuditableEntity
{
    /// <summary>
    /// O nome social preferido pelo titular. Na consulta administrativa é dado pessoal, como todo texto;
    /// o que é público é a forma de identificação nos pontos de publicação, que não passa pela
    /// classificação do fato (ADR-0082).
    /// </summary>
    public const string CodigoDoNomeSocial = "NOME_SOCIAL";

    public string Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    /// <summary>
    /// O que o dado significa, para quem analisa a inscrição: obrigatória, e congelada pela publicação no
    /// fato coletado e no derivado do processo.
    /// </summary>
    public string Descricao { get; private set; } = null!;

    /// <summary>
    /// Como a tela exibe o valor quando o modo de exibição não se deduz do tipo, do formato nem da
    /// classificação; <see langword="null"/> deixa o padrão que a consulta resolve. Apresentação do fato:
    /// mudá-lo não altera a proteção de dados nem reinicia o fato aprovado.
    /// </summary>
    public ComponenteDeExibicao? ComponenteExibicao { get; private set; }
    public TipoDoDado Dominio { get; private set; }
    public OrigemFato Origem { get; private set; }
    public QuantidadeDeValores Cardinalidade { get; private set; }

    /// <summary>
    /// De onde vêm os valores de um fato categórico (ADR-0136) — <see langword="null"/> para
    /// booleano e numérico. Quem consome o fato decide por ela, nunca pelo código do fato.
    /// </summary>
    public FonteValoresFato? FonteValores { get; private set; }

    /// <summary>
    /// Código canônico da fase (<see cref="FaseCanonicaCatalogo"/>) em que o valor
    /// deste fato fica conhecido (ADR-0116). Não é FK — é referência por valor,
    /// como <see cref="Codigo"/> de <c>Modalidade</c>.
    /// </summary>
    public string PontoResolucao { get; private set; } = null!;

    /// <summary>
    /// Referência de onde/como o valor do fato é produzido (ADR-0116), no formato
    /// <c>"{PREFIXO}:{REFERENCIA}"</c> com prefixo coerente com <see cref="Origem"/>.
    /// </summary>
    public string Binding { get; private set; } = null!;

    /// <summary>O formato do texto — só no fato de domínio texto.</summary>
    public FormatoTexto? Formato { get; private set; }

    /// <summary>Sobre quem o fato é respondido: o candidato ou cada membro de um grupo repetível.</summary>
    public EscopoFato Escopo { get; private set; }

    /// <summary>Classificação de proteção de dados na escala da ADR-0081.</summary>
    public ClassificacaoDeProtecao ClassificacaoProtecao { get; private set; }

    /// <summary>Para que o dado é tratado.</summary>
    public string FinalidadeTratamento { get; private set; } = null!;

    /// <summary>Hipótese legal de tratamento, do art. 7º ou do art. 11 da LGPD conforme a classificação.</summary>
    public HipoteseLegalTratamento HipoteseLegal { get; private set; }

    /// <summary>
    /// Fato semeado pelo sistema: o nome, a descrição e o componente de exibição são editáveis
    /// (<see cref="AlterarDescritivo"/>), e a proteção de dados, por <see cref="AlterarDefinicao"/>; a
    /// definição que o sistema calcula ou lê é travada.
    /// </summary>
    public bool SemeadoPeloSistema { get; private set; }

    /// <summary>
    /// O circuito de aprovação da proteção de dados (ADR-0153): o fato só entra na configuração e na
    /// publicação de processo seletivo depois de aprovado, e criar ou mudar a proteção o deixa pendente.
    /// </summary>
    public AprovacaoDaProtecao Aprovacao { get; private set; } = new();

    /// <summary>Fato inativo não recebe vínculo novo; os existentes continuam.</summary>
    public bool Ativo { get; private set; }

    /// <summary>
    /// As regras padrão do derivado por regra do administrador (ADR-0136): <c>{quando, contribui}</c>,
    /// sem ordem, avaliadas por união. São o ponto de partida que o processo copia; a cópia do
    /// processo é a que vale.
    /// </summary>
    public IReadOnlyList<RegraDerivacao> RegrasPadrao { get; private set; } = [];

    /// <summary>
    /// Os fatos de que o derivado do sistema depende (<see cref="DerivadosDoSistema"/>), registrados
    /// pelo seed; vazio nos demais fatos.
    /// </summary>
    public IReadOnlyList<string> Dependencias { get; private set; } = [];

    /// <summary>
    /// O fato de membro que o agregado aponta (ADR-0138), ou <see langword="null"/> quando o fato não
    /// é agregado sobre grupo repetível.
    /// </summary>
    public string? FatoDeMembroAgregado => VinculoDeFato.Nomeado(Binding, MecanismoDoVinculo.ResumoDeGrupo);

    /// <summary>O valor do fato vem da regra de derivação do próprio fato.</summary>
    public bool DerivadoPorRegra =>
        VinculoDeFato.Usa(Binding, MecanismoDoVinculo.RegraDeDerivacao);

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    private readonly List<FatoValorDominio> _valoresDominioDeclarados = [];

    /// <summary>
    /// Os valores de um categórico de fonte global, com a descrição que orienta a escolha do
    /// candidato quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/> (ADR-0116).
    /// Não ordenada aqui (mesmo padrão de <c>AtendimentoEspecializado.Condicoes</c>)
    /// — a ordenação por <c>Ordem</c>/<c>Codigo</c> é responsabilidade de quem
    /// projeta a leitura (<c>FatoCandidatoReader</c>), não do agregado.
    /// </summary>
    public IReadOnlyCollection<FatoValorDominio> ValoresDominioDeclarados => _valoresDominioDeclarados.AsReadOnly();

    // Construtor de materialização do EF Core.
    private FatoCandidato()
    {
    }

    internal FatoCandidato(DescritivoDoFato descritivo, OrigemFato origem, string binding, EixosDaDefinicao eixos, bool sistema)
    {
        Codigo = descritivo.Codigo;
        Nome = descritivo.Nome;
        Descricao = descritivo.Descricao ?? string.Empty;
        ComponenteExibicao = descritivo.ComponenteExibicao;
        Origem = origem;
        Binding = binding;
        AplicarEixos(eixos);
        SemeadoPeloSistema = sistema;
        Ativo = true;
    }

    /// <summary>
    /// Substitui as regras padrão do derivado por regra do administrador, conferidas contra o
    /// catálogo (<see cref="ValidadorRegrasPadrao"/>). Lista vazia remove as regras.
    /// </summary>
    public Result DefinirRegrasPadrao(IReadOnlyList<RegraDerivacao> regras, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(catalogo);

        Result validacao = ConferenciaDasRegrasPadraoDoFato.Conferir(this, regras, catalogo);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        RegrasPadrao = [.. regras];
        Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    /// <summary>
    /// Desativa um valor do fato do administrador: condição nova que o cite é recusada, e a que já o
    /// citava continua. O valor do fato de sistema só muda por nova versão do sistema.
    /// </summary>
    public Result DesativarValor(string codigo) => Valores.DefinirAtivo(codigo, ativo: false);

    /// <summary>Reativa um valor do fato do administrador.</summary>
    public Result ReativarValor(string codigo) => Valores.DefinirAtivo(codigo, ativo: true);

    /// <summary>
    /// Altera o nome, a descrição e o componente de exibição, que são a apresentação do fato e valem
    /// também no fato de sistema. Não tocam a proteção de dados, então não reiniciam o fato aprovado; o fato com
    /// ajuste recomendado ou reprovado volta a pendente, porque a decisão pode ter sido sobre qualquer campo. Os
    /// eixos têm a alteração própria, <see cref="AlterarDefinicao"/>.
    /// </summary>
    public Result AlterarDescritivo(string nome, string? descricao, ComponenteDeExibicao? componenteExibicao)
    {
        Result<(string Nome, string Descricao, ComponenteDeExibicao? Componente)> descritivo =
            ValidarDescritivo(nome, descricao, componenteExibicao);
        if (descritivo.IsFailure)
        {
            return Result.ValidationFailure(descritivo.Errors);
        }

        (Nome, Descricao, ComponenteExibicao) = descritivo.Value;
        Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    /// <summary>
    /// Altera os eixos do fato do administrador — domínio, cardinalidade, fonte dos valores, formato,
    /// escopo, ponto de resolução e proteção de dados — no subconjunto que o cadastro do tipo dele
    /// aceita: o declarado, todos; o derivado por regra, domínio (booleano ou categórico), escopo,
    /// ponto de resolução e proteção; o agregado, só ponto de resolução e proteção. Código, origem e
    /// vínculo não mudam (ADR-0136). As validações são as do cadastro (ADR-0125), mais a coerência do
    /// fato com os valores e as dependências que já tem. O fato não muda quando há recusa.
    /// </summary>
    public Result AlterarDefinicao(DefinicaoDoFato definicao, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(catalogo);

        return SemeadoPeloSistema
            ? RedefinicaoDoFato.DoFatoDeSistema(this, definicao, catalogo)
            : RedefinicaoDoFato.DoAdministrador(this, definicao, catalogo);
    }

    /// <summary>A proteção de dados do fato: a classificação, a finalidade e a hipótese legal.</summary>
    public ProtecaoDeDados Protecao => new(ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);

    internal void AplicarProtecao(ProtecaoDeDados protecao)
    {
        ClassificacaoProtecao = protecao.Classificacao;
        FinalidadeTratamento = protecao.Finalidade;
        HipoteseLegal = protecao.Hipotese;
    }

    /// <summary>Envia o fato pendente à proteção de dados, numa solicitação.</summary>
    public Result SolicitarAprovacaoDaProtecao(string ator, DateTimeOffset em) => Aprovacao.Solicitar(ator, em);

    /// <summary>A proteção de dados aprova a classificação, a finalidade e a hipótese legal do fato.</summary>
    public Result AprovarProtecao(string ator, DateTimeOffset em) => Aprovacao.Aprovar(ator, em);

    /// <summary>
    /// A proteção de dados recomenda o ajuste da proteção do fato e indica a classificação correta, que precisa
    /// respeitar o mínimo do domínio do fato.
    /// </summary>
    public Result RecomendarAjusteDaProtecao(ClassificacaoDeProtecao? recomendada, string? justificativa, string ator, DateTimeOffset em) =>
        TrocaDaProtecaoDoFato.Recomendar(this, recomendada, justificativa, ator, em);

    /// <summary>A proteção de dados reprova a proteção do fato, sem recomendar classificação, e explica o motivo.</summary>
    public Result ReprovarProtecao(string? justificativa, string ator, DateTimeOffset em) => Aprovacao.Reprovar(justificativa, ator, em);

    /// <summary>
    /// O administrador aceita a recomendação da proteção de dados: o fato passa à classificação
    /// recomendada e fica aprovado, sem nova solicitação, inclusive o fato de sistema e o semeado. O
    /// que depende do fato precisa continuar valendo; o ajuste já recusou a recomendação que a
    /// hipótese legal não admite.
    /// </summary>
    public Result AceitarRecomendacaoDaProtecao(string ator, DateTimeOffset em, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return TrocaDaProtecaoDoFato.Aceitar(this, ator, em, catalogo);
    }

    /// <summary>
    /// O que o agregado deixa de ser quando o fato de membro passa a ser <paramref name="membro"/>:
    /// o domínio, a cardinalidade e a fonte que ele gravou são os inferidos do membro, e ele não
    /// protege menos nem resolve antes dele. Vazio quando o agregado continua valendo.
    /// </summary>
    public IReadOnlyList<FieldError> MotivosPelosQuaisDeixaDeAgregar(FatoCandidato membro, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(membro);
        ArgumentNullException.ThrowIfNull(catalogo);

        return MotivosDoAgregado.PelosQuaisDeixaDeAgregar(this, membro, catalogo);
    }

    internal EixosDaDefinicao Eixos => new(
        Dominio, Cardinalidade, FonteValores, Formato, PontoResolucao, Escopo, ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);

    internal void AplicarEixos(EixosDaDefinicao eixos)
    {
        Dominio = eixos.Dominio;
        Cardinalidade = eixos.Cardinalidade;
        FonteValores = eixos.FonteValores;
        Formato = eixos.Formato;
        PontoResolucao = eixos.PontoResolucao;
        Escopo = eixos.Escopo ?? Escopo;
        AplicarProtecao(new ProtecaoDeDados(eixos.Classificacao, eixos.Finalidade, eixos.Hipotese));
    }

    /// <summary>Reativa o fato do administrador, que volta a aceitar vínculos novos.</summary>
    public Result Reativar()
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (Ativo)
        {
            return Result.Failure(new DomainError(FatoCandidatoErrorCodes.JaAtivo, "O fato já está ativo."));
        }

        Ativo = true;
        return Result.Success();
    }

    /// <summary>
    /// Desativa o fato do administrador: vínculos novos são recusados, os existentes continuam. O
    /// fato de sistema não é desativado.
    /// </summary>
    public Result Desativar()
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (!Ativo)
        {
            return Result.Failure(new DomainError(FatoCandidatoErrorCodes.JaDesativado, "O fato já está desativado."));
        }

        Ativo = false;
        return Result.Success();
    }

    /// <summary>
    /// Adiciona um valor ao conjunto fechado de um categórico estático (ADR-0116).
    /// Só o agregado conhece o necessário para validar: que o próprio
    /// <see cref="Dominio"/> é <see cref="TipoDoDado.Opcao"/>, que o
    /// <paramref name="codigo"/> (normalizado por trim, comparação ordinal) não
    /// colide com um irmão já adicionado, e que a <paramref name="descricao"/> é
    /// obrigatória quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>. A
    /// <paramref name="orientacao"/>, opcional, é o que o candidato vê abaixo da opção.
    /// </summary>
    public Result AdicionarValorDominio(string codigo, string? descricao, int ordem, bool ativo, string? orientacao = null) =>
        Valores.Adicionar(codigo, descricao, ordem, ativo, orientacao);

    /// <summary>
    /// Corrige a descrição e a orientação de um valor do fato do administrador, com as mesmas regras
    /// do acréscimo. O código do valor não muda: mudar o sentido de uma opção é acrescentar outro
    /// valor e desativar o antigo.
    /// </summary>
    public Result CorrigirValor(string codigo, string? descricao, string? orientacao) =>
        Valores.Corrigir(codigo, descricao, orientacao);

    /// <summary>
    /// Reordena os valores do fato do administrador pela lista completa de códigos — ativos e
    /// desativados —, na ordem em que passam a aparecer. A lista que deixa um valor de fora, repete
    /// um código ou cita um código que o fato não tem é recusada inteira, e as três faltas acumulam.
    /// </summary>
    public Result ReordenarValores(IReadOnlyList<string> codigosEmOrdem)
    {
        ArgumentNullException.ThrowIfNull(codigosEmOrdem);

        return Valores.Reordenar(codigosEmOrdem);
    }

    private ValoresDoDominioDoFato Valores => new(this, _valoresDominioDeclarados);

    private DomainError? RecusaSeSistema() => SemeadoPeloSistema ? RedefinicaoDoFatoDeSistema.RecusaDaDefinicao : null;

}
