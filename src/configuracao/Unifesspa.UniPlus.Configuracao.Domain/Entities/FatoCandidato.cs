namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;
using static Unifesspa.UniPlus.Configuracao.Domain.Entities.CriacaoDoFato;

/// <summary>
/// Um fato do candidato no catálogo <c>fatos_do_candidato</c> (UNI-REQ-0143, ADR-0136):
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
    public DominioFato Dominio { get; private set; }
    public OrigemFato Origem { get; private set; }
    public CardinalidadeFato Cardinalidade { get; private set; }

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
    /// Fato semeado pelo sistema: só o nome, a descrição e a proteção de dados são editáveis; a
    /// definição que o sistema calcula ou lê é travada.
    /// </summary>
    public bool Sistema { get; private set; }

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
    public string? FatoDeMembroAgregado => VinculoDeFato.Nomeado(Binding, PrefixoBindingAgregacao);

    /// <summary>O valor do fato vem da regra de derivação do próprio fato.</summary>
    public bool DerivadoPorRegra =>
        Binding.StartsWith(PrefixoBindingDerivadoRegra + ":", StringComparison.Ordinal);

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

    internal FatoCandidato(
        string codigo,
        string nome,
        string descricao,
        ComponenteDeExibicao? componenteExibicao,
        DominioFato dominio,
        OrigemFato origem,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato,
        string pontoResolucao,
        string binding,
        EscopoFato escopo,
        ProtecaoDeDados protecao,
        bool sistema)
    {
        Formato = formato;
        Codigo = codigo;
        FonteValores = fonteValores;
        Nome = nome;
        Descricao = descricao;
        ComponenteExibicao = componenteExibicao;
        Dominio = dominio;
        Origem = origem;
        Cardinalidade = cardinalidade;
        PontoResolucao = pontoResolucao;
        Binding = binding;
        Escopo = escopo;
        ClassificacaoProtecao = protecao.Classificacao;
        FinalidadeTratamento = protecao.Finalidade;
        HipoteseLegal = protecao.Hipotese;
        Sistema = sistema;
        Ativo = true;
    }

    /// <summary>
    /// Factory canônica: valida o código, o nome, o domínio, a origem, a cardinalidade, a fonte
    /// dos valores, o formato, o ponto de resolução, o vínculo, o escopo e a proteção de dados. As
    /// violações independentes são acumuladas (ADR-0125); a que depende de outro campo só é
    /// conferida quando esse campo é válido.
    /// </summary>
    public static Result<FatoCandidato> Criar(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        OrigemFato origem,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato,
        string pontoResolucao,
        string binding,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        bool sistema,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.Criar(
            codigo, nome, descricao, dominio, origem, cardinalidade, fonteValores, formato, pontoResolucao, binding,
            escopo, classificacaoProtecao, finalidadeTratamento, hipoteseLegal, sistema, componenteExibicao);

    /// <summary>
    /// Cadastro de fato pelo administrador (ADR-0136): sempre declarado, com o vínculo ao campo de
    /// formulário gerado a partir do código, e nunca de sistema.
    /// </summary>
    public static Result<FatoCandidato> CriarDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        CardinalidadeFato cardinalidade,
        FonteValoresFato? fonteValores,
        FormatoTexto? formato,
        string pontoResolucao,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.CriarDoAdministrador(
            codigo, nome, descricao, dominio, cardinalidade, fonteValores, formato, pontoResolucao, escopo,
            classificacaoProtecao, finalidadeTratamento, hipoteseLegal, componenteExibicao);

    /// <summary>
    /// Cadastro de derivado por regra pelo administrador (ADR-0136): booleano, verdadeiro quando
    /// alguma regra ativa, ou categórico de valores próprios, com a união das contribuições. As
    /// regras padrão vêm depois, por <see cref="DefinirRegrasPadrao"/>, porque citam os valores que
    /// o categórico ainda vai receber.
    /// </summary>
    public static Result<FatoCandidato> CriarDerivadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        DominioFato dominio,
        string pontoResolucao,
        EscopoFato escopo,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            codigo, nome, descricao, dominio, pontoResolucao, escopo, classificacaoProtecao, finalidadeTratamento,
            hipoteseLegal, componenteExibicao);

    /// <summary>
    /// Cadastro do agregado sobre grupo repetível pelo administrador (ADR-0138, UNI-REQ-0146): o
    /// vínculo <c>AGREGACAO_GRUPO:</c> aponta o fato de membro, que precisa existir, estar ativo e ser
    /// fato declarado de membro de grupo de domínio que agrega. O domínio, a cardinalidade e a fonte
    /// dos valores saem do fato de membro — booleano dá "existe membro que…", categórico dá "valores
    /// presentes" —, e o agregado não tem valores próprios. Não protege menos nem resolve antes do
    /// fato de membro. As recusas se acumulam (ADR-0125); sem fato de membro válido, as que
    /// dependem dele não são conferidas.
    /// </summary>
    public static Result<FatoCandidato> CriarAgregadoDoAdministrador(
        string codigo,
        string nome,
        string? descricao,
        string fatoDeMembroCodigo,
        CatalogoDeFatos catalogo,
        string pontoResolucao,
        ClassificacaoDeProtecao classificacaoProtecao,
        string finalidadeTratamento,
        HipoteseLegalTratamento hipoteseLegal,
        ComponenteDeExibicao? componenteExibicao = null) =>
        CriacaoDoFato.CriarAgregadoDoAdministrador(
            codigo, nome, descricao, fatoDeMembroCodigo, catalogo, pontoResolucao, classificacaoProtecao,
            finalidadeTratamento, hipoteseLegal, componenteExibicao);


    /// <summary>
    /// Substitui as regras padrão do derivado por regra do administrador, conferidas contra o
    /// catálogo (<see cref="ValidadorRegrasPadrao"/>). Lista vazia remove as regras.
    /// </summary>
    public Result DefinirRegrasPadrao(IReadOnlyList<RegraDerivacao> regras, CatalogoDeFatos catalogo)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (!DerivadoPorRegra)
        {
            return Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.RegrasPadraoSoEmDerivadoPorRegra,
                "Só o fato derivado por regra tem regras padrão."));
        }

        Result validacao = ValidadorRegrasPadrao.Validar(this, regras, catalogo);
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
    public Result DesativarValor(string codigo) => DefinirAtivoDoValor(codigo, ativo: false);

    /// <summary>Reativa um valor do fato do administrador.</summary>
    public Result ReativarValor(string codigo) => DefinirAtivoDoValor(codigo, ativo: true);

    private Result DefinirAtivoDoValor(string codigo, bool ativo)
    {
        if (RecusaSeSistema() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        if (ValorPorCodigo(codigo) is not { } valor)
        {
            return Result.Failure(ValorNaoEncontrado);
        }

        if (valor.Ativo == ativo)
        {
            return Result.Failure(ativo
                ? new DomainError(FatoValorDominioErrorCodes.JaAtivo, "O valor já está ativo.")
                : new DomainError(FatoValorDominioErrorCodes.JaDesativado, "O valor já está desativado."));
        }

        valor.DefinirAtivo(ativo);
        Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

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

        if (Sistema)
        {
            return AlterarProtecaoDoFatoDeSistema(definicao, catalogo);
        }

        ProtecaoDeDados protecaoAnterior = Protecao;
        bool agregado = FatoDeMembroAgregado is not null;
        bool derivado = DerivadoPorRegra;
        List<FieldError> erros = [];
        void Recusar(string campo, string codigoErro, string mensagem) => erros.Add(new(campo, new DomainError(codigoErro, mensagem)));
        void RecusarSeInformado(string campo, bool informado)
        {
            if (informado)
            {
                Recusar(campo, FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato,
                    agregado
                        ? "O agregado infere este eixo do fato de membro; só o ponto de resolução e a proteção de dados se alteram."
                        : "O fato derivado por regra infere este eixo do domínio; não o informe.");
            }
        }

        DominioFato dominio = Dominio;
        CardinalidadeFato cardinalidade = Cardinalidade;
        FonteValoresFato? fonteValores = FonteValores;
        FormatoTexto? formato = Formato;
        EscopoFato escopo = Escopo;
        bool eixosDeValorValidos = true;
        // Só o domínio aplicável confere a proteção mínima: o recusado (fora do vocabulário, ou texto
        // num derivado) nunca será gravado, e a recusa dele já foi dada.
        bool dominioAplicavel = true;

        if (agregado)
        {
            RecusarSeInformado("dominio", definicao.Dominio is not null);
            RecusarSeInformado("cardinalidade", definicao.Cardinalidade is not null);
            RecusarSeInformado("fonteValores", definicao.FonteValores is not null);
            RecusarSeInformado("formato", definicao.Formato is not null);
            RecusarSeInformado("escopo", definicao.Escopo is not null);
        }
        else
        {
            dominio = definicao.Dominio ?? DominioFato.Nenhum;
            escopo = definicao.Escopo ?? EscopoFato.Nenhum;
            ConferirEscopo(erros, escopo);
            if (derivado)
            {
                RecusarSeInformado("cardinalidade", definicao.Cardinalidade is not null);
                RecusarSeInformado("fonteValores", definicao.FonteValores is not null);
                RecusarSeInformado("formato", definicao.Formato is not null);
                (cardinalidade, fonteValores) = EixosInferidosDoDerivado(dominio);
                formato = null;
                if (!ConferirDominioDoDerivado(erros, dominio))
                {
                    eixosDeValorValidos = false;
                    dominioAplicavel = false;
                }
            }
            else
            {
                cardinalidade = definicao.Cardinalidade ?? CardinalidadeFato.Nenhuma;
                fonteValores = definicao.FonteValores;
                formato = definicao.Formato;
                int antes = erros.Count;
                ConferirEixosDeValor(erros, dominio, cardinalidade, fonteValores, formato);
                eixosDeValorValidos = erros.Count == antes;
                dominioAplicavel = dominio != DominioFato.Nenhum && Enum.IsDefined(dominio);
            }

            if (eixosDeValorValidos && _valoresDominioDeclarados.Count > 0)
            {
                // Os valores do catálogo só existem no categórico de fonte global: o eixo novo que
                // não os comporta é recusado, e o valor não é descartado em silêncio.
                if (dominio != DominioFato.Categorico)
                {
                    eixosDeValorValidos = false;
                    Recusar("dominio", FatoCandidatoErrorCodes.DominioNaoComportaOsValores,
                        "O fato já tem valores cadastrados e só o categórico os tem; desativar os valores não basta, porque o código de um valor nunca é removido.");
                }
                else if (fonteValores != FonteValoresFato.Global)
                {
                    eixosDeValorValidos = false;
                    Recusar("fonteValores", FatoCandidatoErrorCodes.FonteNaoComportaOsValores,
                        "O fato já tem valores cadastrados no catálogo e só a fonte global os tem.");
                }
            }
        }

        Result<string> ponto = ValidarPontoResolucao(definicao.PontoResolucao);
        if (ponto.IsFailure)
        {
            erros.Add(new("pontoResolucao", ponto.Error!));
        }

        Result<ProtecaoDeDados> protecao = ValidarProtecao(
            dominioAplicavel ? dominio : null, definicao.ClassificacaoProtecao, definicao.FinalidadeTratamento, definicao.HipoteseLegal);
        erros.AddRange(protecao.Errors);

        // A coerência com as dependências do próprio fato é conferida com o que há de válido na
        // entrada; o eixo recusado acima fica como está, e a recusa dele já foi dada.
        EixosDaDefinicao anteriores = new(
            Dominio, Cardinalidade, FonteValores, Formato, PontoResolucao, Escopo,
            ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);
        EixosDaDefinicao propostos = new(
            eixosDeValorValidos ? dominio : Dominio,
            eixosDeValorValidos ? cardinalidade : Cardinalidade,
            eixosDeValorValidos ? fonteValores : FonteValores,
            eixosDeValorValidos ? formato : Formato,
            ponto.IsSuccess ? ponto.Value! : PontoResolucao,
            erros.Any(static e => e.Field == "escopo") ? null : escopo,
            protecao.IsSuccess ? protecao.Value.Classificacao : ClassificacaoProtecao,
            protecao.IsSuccess ? protecao.Value.Finalidade : FinalidadeTratamento,
            protecao.IsSuccess ? protecao.Value.Hipotese : HipoteseLegal);
        // Só a recusa que a alteração causa conta: a que já existia antes dela não a trava.
        List<FieldError> dependenciasAntes = [];
        ConferirComAsDependencias(dependenciasAntes, catalogo, agregado, derivado, true, true);
        Aplicar(propostos);
        List<FieldError> dependenciasDepois = [];
        ConferirComAsDependencias(dependenciasDepois, catalogo, agregado, derivado, ponto.IsSuccess, protecao.IsSuccess);
        erros.AddRange(DiferencaDeRecusas.Novas(dependenciasDepois, dependenciasAntes));

        if (erros.Count > 0)
        {
            Aplicar(anteriores);
            return Result.ValidationFailure(erros);
        }

        Aprovacao.AoEditarItem(Protecao != protecaoAnterior);
        return Result.Success();
    }

    /// <summary>A proteção de dados do fato: a classificação, a finalidade e a hipótese legal.</summary>
    public ProtecaoDeDados Protecao => new(ClassificacaoProtecao, FinalidadeTratamento, HipoteseLegal);

    /// <summary>
    /// No fato de sistema só a proteção de dados muda pela tela: a definição que o sistema calcula ou
    /// lê é travada, e a alteração que a toca é recusada inteira. Os eixos não informados são ignorados;
    /// os informados precisam ser os que o fato já tem.
    /// </summary>
    private Result AlterarProtecaoDoFatoDeSistema(DefinicaoDoFato definicao, CatalogoDeFatos catalogo)
    {
        // O token que o vocabulário não conhece é erro de vocabulário no campo, e não troca de definição travada.
        List<FieldError> foraDoVocabulario = [];
        if (definicao.Dominio is { } dominioInformado && !Enum.IsDefined(dominioInformado))
        {
            foraDoVocabulario.Add(new("dominio", new DomainError(FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado.")));
        }

        if (definicao.Cardinalidade is { } cardinalidadeInformada && !Enum.IsDefined(cardinalidadeInformada))
        {
            foraDoVocabulario.Add(new("cardinalidade", new DomainError(FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado.")));
        }

        if (definicao.Escopo is { } escopoInformado && !Enum.IsDefined(escopoInformado))
        {
            foraDoVocabulario.Add(new("escopo", new DomainError(FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato fora do vocabulário fechado.")));
        }

        if (definicao.FonteValores is { } fonteInformada && !Enum.IsDefined(fonteInformada))
        {
            foraDoVocabulario.Add(new("fonteValores", new DomainError(FatoCandidatoErrorCodes.FonteValoresObrigatoria, "Fonte dos valores do fato fora do vocabulário fechado.")));
        }

        if (definicao.Formato is { } formatoInformado && !Enum.IsDefined(formatoInformado))
        {
            foraDoVocabulario.Add(new("formato", new DomainError(FatoCandidatoErrorCodes.FormatoObrigatorio, "Formato do fato fora do vocabulário fechado.")));
        }

        if (foraDoVocabulario.Count > 0)
        {
            return Result.ValidationFailure(foraDoVocabulario);
        }

        bool tocaNaDefinicao =
            (definicao.Dominio is { } dominio && dominio != Dominio)
            || (definicao.Cardinalidade is { } cardinalidade && cardinalidade != Cardinalidade)
            || (definicao.FonteValores is { } fonte && fonte != FonteValores)
            || (definicao.Formato is { } formato && formato != Formato)
            || (definicao.Escopo is { } escopo && escopo != Escopo)
            || (!string.IsNullOrWhiteSpace(definicao.PontoResolucao)
                && !string.Equals(definicao.PontoResolucao.Trim(), PontoResolucao, StringComparison.Ordinal));
        if (tocaNaDefinicao)
        {
            return Result.Failure(RecusaDaDefinicaoDoFatoDeSistema);
        }

        ProtecaoDeDados anterior = Protecao;
        Result troca = TrocarProtecao(definicao.ClassificacaoProtecao, definicao.FinalidadeTratamento, definicao.HipoteseLegal, catalogo);
        if (troca.IsFailure)
        {
            return troca;
        }

        Aprovacao.AoEditarItem(Protecao != anterior);
        return Result.Success();
    }

    /// <summary>
    /// Troca a proteção do fato conferindo o que ele próprio exige — o mínimo do domínio e a coerência da
    /// hipótese com a classificação — e o que depende dele: só a recusa que a troca causa conta (ADR-0125).
    /// O fato não muda quando há recusa. Quem chama decide o que a troca faz com a aprovação.
    /// </summary>
    private Result TrocarProtecao(
        ClassificacaoDeProtecao classificacao, string finalidade, HipoteseLegalTratamento hipotese, CatalogoDeFatos catalogo)
    {
        Result<ProtecaoDeDados> protecao = ValidarProtecao(Dominio, classificacao, finalidade, hipotese);
        if (protecao.IsFailure)
        {
            return Result.ValidationFailure(protecao.Errors);
        }

        bool agregado = FatoDeMembroAgregado is not null;
        bool derivado = DerivadoPorRegra;
        ProtecaoDeDados anterior = Protecao;
        List<FieldError> dependenciasAntes = [];
        ConferirComAsDependencias(dependenciasAntes, catalogo, agregado, derivado, true, true);

        AplicarProtecao(protecao.Value);
        List<FieldError> dependenciasDepois = [];
        ConferirComAsDependencias(dependenciasDepois, catalogo, agregado, derivado, true, true);
        List<FieldError> novas = [.. DiferencaDeRecusas.Novas(dependenciasDepois, dependenciasAntes)];
        if (novas.Count == 0)
        {
            return Result.Success();
        }

        AplicarProtecao(anterior);
        return Result.ValidationFailure(novas);
    }

    private void AplicarProtecao(ProtecaoDeDados protecao)
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
    public Result RecomendarAjusteDaProtecao(ClassificacaoDeProtecao recomendada, string? justificativa, string ator, DateTimeOffset em)
    {
        Result recusa = AprovacaoDaProtecao.ConferirRecomendacao(recomendada, ClassificacaoProtecao, HipoteseLegal);
        if (recusa.IsFailure)
        {
            return recusa;
        }

        Result<ProtecaoDeDados> noMinimoDoDominio = ValidarProtecao(
            Dominio, recomendada, FinalidadeTratamento, HipoteseLegal);
        if (noMinimoDoDominio.Errors.FirstOrDefault(static e => e.Field == "classificacaoProtecao") is { } abaixo)
        {
            return Result.ValidationFailure([abaixo with { Field = "classificacaoRecomendada" }]);
        }

        return Aprovacao.RecomendarAjuste(recomendada, justificativa, ator, em);
    }

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

        Result<ClassificacaoDeProtecao> recomendada = Aprovacao.RecomendacaoAAceitar();
        if (recomendada.IsFailure)
        {
            return Result.Failure(recomendada.Error!);
        }

        Result troca = TrocarProtecao(recomendada.Value, FinalidadeTratamento, HipoteseLegal, catalogo);
        if (troca.IsFailure)
        {
            return troca;
        }

        return Aprovacao.AceitarRecomendacao(recomendada.Value, ator, em);
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

        List<FieldError> motivos = [];
        static FieldError Motivo(string motivo, string mensagem) =>
            new($"agregado.{motivo}", new DomainError(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato, mensagem));
        OperacaoAgregado operacao = AgregadoDeGrupo.OperacaoDoDominio(DominiosFato.ParaTokenCanonico(membro.Dominio));
        if (membro is not { Escopo: EscopoFato.MembroGrupo, Origem: OrigemFato.Declarado })
        {
            motivos.Add(Motivo("membro", $"'{membro.Codigo}' deixa de ser fato declarado de membro de grupo, e o agregado resume o que cada membro respondeu."));
            return motivos;
        }

        if (operacao == OperacaoAgregado.Nenhuma)
        {
            motivos.Add(Motivo("dominioSemResumo", $"'{membro.Codigo}' passa a um domínio que o agregado não resume (só booleano e categórico)."));
            return motivos;
        }

        if (!membro.Ativo)
        {
            motivos.Add(Motivo("membroInativo", $"'{membro.Codigo}' está desativado no catálogo, e o agregado resume um fato de membro ativo."));
        }

        (DominioFato dominioEsperado, CardinalidadeFato cardinalidadeEsperada, FonteValoresFato? fonteEsperada) =
            EixosInferidosDoAgregado(operacao, membro.FonteValores);
        if (Dominio != dominioEsperado)
        {
            motivos.Add(Motivo("dominio", $"o domínio de '{membro.Codigo}' mudou, e o agregado gravou o domínio {DominiosFato.ParaTokenCanonico(Dominio)} inferido dele."));
        }
        else if (Cardinalidade != cardinalidadeEsperada)
        {
            motivos.Add(Motivo("cardinalidade", $"a cardinalidade do agregado não acompanha a inferida de '{membro.Codigo}': o agregado gravou {CardinalidadesFato.ParaTokenCanonico(Cardinalidade)}."));
        }
        else if (FonteValores != fonteEsperada)
        {
            motivos.Add(Motivo("fonte", $"a fonte dos valores de '{membro.Codigo}' mudou, e o agregado gravou a fonte inferida dela."));
        }

        List<FieldError> erros = [];
        ConferirContraOMembro(erros, membro, PontoResolucao, ClassificacaoProtecao, catalogo);
        motivos.AddRange(erros);
        return motivos;
    }

    private void ConferirComAsDependencias(
        List<FieldError> erros, CatalogoDeFatos catalogo, bool agregado, bool derivado, bool pontoValido, bool protecaoValida)
    {
        if (agregado)
        {
            FatoCandidato? membro = catalogo.Fatos.FirstOrDefault(f => string.Equals(f.Codigo, FatoDeMembroAgregado, StringComparison.Ordinal));
            if (membro is not null && (pontoValido || protecaoValida))
            {
                List<FieldError> contraOMembro = [];
                ConferirContraOMembro(contraOMembro, membro, PontoResolucao, ClassificacaoProtecao, catalogo);
                erros.AddRange(contraOMembro.Where(e => e.Field == "pontoResolucao" ? pontoValido : protecaoValida));
            }

            return;
        }

        if (derivado && RegrasPadrao.Count > 0)
        {
            // As regras padrão são as do fato, e a mudança de domínio, de ponto de resolução ou de
            // proteção pode deixá-las sem valer. A recusa vai no eixo que a causou, não na regra.
            Result regras = ValidadorRegrasPadrao.Validar(this, RegrasPadrao, catalogo);
            erros.AddRange(regras.Errors.Select(static e => e.Error.Code switch
            {
                FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia => e with { Field = "classificacaoProtecao" },
                FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia => e with { Field = "pontoResolucao" },
                _ => e with { Field = "dominio" },
            }).Where(e => e.Field switch
            {
                "classificacaoProtecao" => protecaoValida,
                "pontoResolucao" => pontoValido,
                _ => true,
            }));
        }
    }

    private void Aplicar(EixosDaDefinicao eixos)
    {
        Dominio = eixos.Dominio;
        Cardinalidade = eixos.Cardinalidade;
        FonteValores = eixos.FonteValores;
        Formato = eixos.Formato;
        PontoResolucao = eixos.PontoResolucao;
        Escopo = eixos.Escopo ?? Escopo;
        ClassificacaoProtecao = eixos.Classificacao;
        FinalidadeTratamento = eixos.Finalidade;
        HipoteseLegal = eixos.Hipotese;
    }

    private readonly record struct EixosDaDefinicao(
        DominioFato Dominio,
        CardinalidadeFato Cardinalidade,
        FonteValoresFato? FonteValores,
        FormatoTexto? Formato,
        string PontoResolucao,
        EscopoFato? Escopo,
        ClassificacaoDeProtecao Classificacao,
        string Finalidade,
        HipoteseLegalTratamento Hipotese);

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
    /// <see cref="Dominio"/> é <see cref="DominioFato.Categorico"/>, que o
    /// <paramref name="codigo"/> (normalizado por trim, comparação ordinal) não
    /// colide com um irmão já adicionado, e que a <paramref name="descricao"/> é
    /// obrigatória quando <see cref="Origem"/> é <see cref="OrigemFato.Declarado"/>. A
    /// <paramref name="orientacao"/>, opcional, é o que o candidato vê abaixo da opção.
    /// </summary>
    public Result AdicionarValorDominio(string codigo, string? descricao, int ordem, bool ativo, string? orientacao = null)
    {
        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Result<NovoValorDoFato> novo = ConferenciaDosValoresDoFato.ConferirNovo(codigo, descricao, orientacao, ordem, Origem, CodigosDosValores);
        if (novo.IsFailure)
        {
            return Result.ValidationFailure(novo.Errors);
        }

        _valoresDominioDeclarados.Add(FatoValorDominio.Criar(Id, novo.Value.Codigo, novo.Value.Textos.Descricao, novo.Value.Ordem, ativo, novo.Value.Textos.Orientacao));
        Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    /// <summary>
    /// Corrige a descrição e a orientação de um valor do fato do administrador, com as mesmas regras
    /// do acréscimo. O código do valor não muda: mudar o sentido de uma opção é acrescentar outro
    /// valor e desativar o antigo.
    /// </summary>
    public Result CorrigirValor(string codigo, string? descricao, string? orientacao)
    {
        Result<FatoValorDominio> alvo = ValorEditavel(codigo);
        if (alvo.IsFailure)
        {
            return Result.Failure(alvo.Error!);
        }
        Result<TextosDoValor> textos = ConferenciaDosValoresDoFato.ConferirTextos(Origem, descricao, orientacao);
        if (textos.IsFailure)
        {
            return Result.ValidationFailure(textos.Errors);
        }
        alvo.Value!.CorrigirTextos(textos.Value.Descricao, textos.Value.Orientacao);
        Aprovacao.AoEditarItem(protecaoMudou: false);
        return Result.Success();
    }

    /// <summary>
    /// Reordena os valores do fato do administrador pela lista completa de códigos — ativos e
    /// desativados —, na ordem em que passam a aparecer. A lista que deixa um valor de fora, repete
    /// um código ou cita um código que o fato não tem é recusada inteira, e as três faltas acumulam.
    /// </summary>
    public Result ReordenarValores(IReadOnlyList<string> codigosEmOrdem)
    {
        ArgumentNullException.ThrowIfNull(codigosEmOrdem);
        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result.Failure(recusa);
        }
        Result<string[]> ordem = ConferenciaDosValoresDoFato.ConferirOrdem(codigosEmOrdem, CodigosDosValores);
        if (ordem.IsFailure)
        {
            return Result.ValidationFailure(ordem.Errors);
        }

        AplicarOrdemDosValores(ordem.Value!);
        return Result.Success();
    }

    private IReadOnlyCollection<string> CodigosDosValores => [.. _valoresDominioDeclarados.Select(static v => v.Codigo)];

    // Valor na posição em que já está não é regravado: sem mudança, o EF não marca a linha e a
    // versão do fato não avança.
    private void AplicarOrdemDosValores(string[] codigosEmOrdem)
    {
        foreach ((int posicao, string codigo) in codigosEmOrdem.Index())
        {
            ValorPorCodigo(codigo)!.DefinirOrdem(posicao);
        }

        Aprovacao.AoEditarItem(protecaoMudou: false);
    }

    private Result<FatoValorDominio> ValorEditavel(string codigo)
    {
        if (RecusaDeEstadoParaValores() is { } recusa)
        {
            return Result<FatoValorDominio>.Failure(recusa);
        }

        return ValorPorCodigo(codigo) is { } valor
            ? Result<FatoValorDominio>.Success(valor)
            : Result<FatoValorDominio>.Failure(ValorNaoEncontrado);
    }

    private DomainError? RecusaDeEstadoParaValores() =>
        ConferenciaDosValoresDoFato.RecusaDeEstado(RecusaSeSistema(), Dominio, FatoDeMembroAgregado, FonteValores);

    private static readonly DomainError ValorNaoEncontrado =
        new(FatoValorDominioErrorCodes.NaoEncontrado, "Valor de domínio não encontrado neste fato.");

    private FatoValorDominio? ValorPorCodigo(string codigo) =>
        _valoresDominioDeclarados.FirstOrDefault(v => string.Equals(v.Codigo, codigo?.Trim(), StringComparison.Ordinal));

    private DomainError? RecusaSeSistema() => Sistema ? RecusaDaDefinicaoDoFatoDeSistema : null;

    private static readonly DomainError RecusaDaDefinicaoDoFatoDeSistema = new(
        FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada,
        "Fato de sistema só tem editáveis o nome, a descrição, o componente de exibição e a proteção de dados (classificação, finalidade e hipótese legal). "
        + "Código, domínio, cardinalidade, origem, fonte e formato dos valores, valores de domínio, regras padrão, ponto de resolução, "
        + "escopo e ativação continuam travados, porque o sistema calcula ou lê a definição.");

}
